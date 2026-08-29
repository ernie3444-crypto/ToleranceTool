using System;
using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Datasheet;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Precision;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>
    /// Runs Apply / Check over a datasheet: resolve each row's signal, pick the
    /// tolerance, calculate, then either write the Tolerance column (Apply) or
    /// compare and comment the mismatches (Check).
    /// </summary>
    public sealed class DatasheetRunner
    {
        public const string CommentMarker = "[ToleranceTool]";

        private readonly SignalResolver _resolver;
        private readonly ToleranceLibrary _tolerances;
        private readonly IToleranceEngine _engine;

        public DatasheetRunner(SignalResolver resolver, ToleranceLibrary tolerances, ScaleCurveLibrary curves)
            : this(resolver, tolerances, new ToleranceEngine(curves))
        {
        }

        public DatasheetRunner(SignalResolver resolver, ToleranceLibrary tolerances, IToleranceEngine engine)
        {
            _resolver = resolver;
            _tolerances = tolerances;
            _engine = engine;
        }

        public DatasheetRunResult Run(IDatasheet sheet, DatasheetMapping mapping, DatasheetRunMode mode)
        {
            var result = new DatasheetRunResult { Mode = mode, ToleranceMultiplier = mapping.ToleranceMultiplier };

            if (mapping.Orientation == DatasheetOrientation.ColumnPerCase)
            {
                sheet = new TransposedDatasheet(sheet);
            }

            foreach (DatasheetParameter missing in mapping.MissingRequiredHeaders())
            {
                result.SetupProblems.Add($"The {missing} header is not mapped.");
            }

            if (result.SetupProblems.Count > 0)
            {
                return result;
            }

            string?[] headerRow = sheet.Row(mapping.HeaderRowIndex);
            var columns = new ColumnResolver(mapping, headerRow);
            foreach (ConfigIssue issue in columns.Issues)
            {
                result.SetupProblems.Add(issue.Message);
            }

            if (columns.HasErrors)
            {
                return result;
            }

            int systemIdColumn = columns.Require(DatasheetParameter.SystemId);
            IReadOnlyList<int> expectedColumns = columns.Columns(DatasheetParameter.Expected);
            IReadOnlyList<int> toleranceColumns = columns.Columns(DatasheetParameter.Tolerance);
            int? unitColumn = columns.UnitColumnIndex;

            int blocks = columns.TestPointCount;
            result.TestPointsPerRow = blocks;
            if (expectedColumns.Count != toleranceColumns.Count)
            {
                result.Warnings.Add(
                    $"{expectedColumns.Count} Expected column(s) but {toleranceColumns.Count} Tolerance column(s); using the first {blocks}");
            }

            if (mode == DatasheetRunMode.Apply)
            {
                sheet.ClearToolComments(CommentMarker);
            }

            int firstRow = mapping.FirstDataRowIndex ?? mapping.HeaderRowIndex + 1;
            int lastRow = mapping.LastDataRowIndex ?? sheet.LastRowIndex;

            for (int row = firstRow; row <= lastRow; row++)
            {
                string? systemId = sheet.GetText(row, systemIdColumn)?.Trim();
                if (string.IsNullOrEmpty(systemId))
                {
                    continue;
                }

                SignalResolution resolution = _resolver.Resolve(systemId!);

                if (resolution.Step == ResolutionStep.Excluded)
                {
                    result.Rows.Add(new RowOutcome
                    {
                        RowIndex = row,
                        SystemId = systemId!,
                        Resolution = resolution.Step,
                        Status = RowStatus.Skipped,
                        Note = "Excluded by the resolution review",
                    });
                    continue;
                }

                if (!resolution.IsResolved)
                {
                    string note = resolution.Step == ResolutionStep.Ambiguous
                        ? $"System ID matches several signals: {string.Join(", ", resolution.Candidates)}"
                        : "System ID did not resolve to a signal";
                    result.Rows.Add(new RowOutcome
                    {
                        RowIndex = row,
                        SystemId = systemId!,
                        Resolution = resolution.Step,
                        Status = resolution.Step == ResolutionStep.Ambiguous ? RowStatus.AmbiguousSignal : RowStatus.NoSignal,
                        Note = note,
                    });
                    CommentIfChecking(sheet, mode, row, toleranceColumns[0], note);
                    continue;
                }

                SignalConfig signal = resolution.Signal!;

                if (!_tolerances.TryGet(signal.SignalType, signal.ModuleType, out ToleranceDefinition tolerance))
                {
                    string note = $"No tolerance for {signal.SignalType} / {signal.ModuleType}";
                    result.Rows.Add(new RowOutcome
                    {
                        RowIndex = row,
                        SystemId = systemId!,
                        Resolution = resolution.Step,
                        Status = RowStatus.NoTolerance,
                        Note = note,
                    });
                    CommentIfChecking(sheet, mode, row, toleranceColumns[0], note);
                    continue;
                }

                if (!tolerance.IsEuOnly && signal.RawSpan == 0)
                {
                    string note =
                        $"Signal \"{signal.SensorName}\" (type {signal.SignalType}) has no raw range, " +
                        "which this tolerance needs. Add that signal type to the Signal Type Registry, " +
                        "or map Raw Low / Raw High in the import.";
                    result.Rows.Add(new RowOutcome
                    {
                        RowIndex = row,
                        SystemId = systemId!,
                        Resolution = resolution.Step,
                        Status = RowStatus.NotCalculable,
                        Note = note,
                    });
                    CommentIfChecking(sheet, mode, row, toleranceColumns[0], note);
                    continue;
                }

                UnitSystem unitSystem = RowUnitSystem(sheet, row, unitColumn, mapping.DefaultUnitSystem);

                for (int b = 0; b < blocks; b++)
                {
                    int expectedColumn = expectedColumns[b];
                    int toleranceColumn = toleranceColumns[b];

                    double? expected = sheet.GetNumber(row, expectedColumn);
                    if (expected == null)
                    {
                        string? raw = sheet.GetText(row, expectedColumn)?.Trim();
                        if (string.IsNullOrEmpty(raw))
                        {
                            continue; // empty test point — normal on a wide datasheet
                        }

                        var bad = new RowOutcome
                        {
                            RowIndex = row,
                            TestPoint = b + 1,
                            SystemId = systemId!,
                            Resolution = resolution.Step,
                            Status = RowStatus.NotCalculable,
                            Note = $"Expected value \"{raw}\" is not a number",
                        };
                        result.Rows.Add(bad);
                        CommentIfChecking(sheet, mode, row, toleranceColumn, bad.Note);
                        continue;
                    }

                    var outcome = new RowOutcome
                    {
                        RowIndex = row,
                        TestPoint = b + 1,
                        SystemId = systemId!,
                        Resolution = resolution.Step,
                    };
                    result.Rows.Add(outcome);

                    ToleranceResult calc = _engine.Calculate(expected.Value, unitSystem, signal, tolerance);
                    if (!calc.IsCalculated)
                    {
                        outcome.Status = RowStatus.NotCalculable;
                        outcome.Note = calc.Message ?? calc.Outcome.ToString();
                        CommentIfChecking(sheet, mode, row, toleranceColumn, outcome.Note);
                        continue;
                    }

                    double factor = mapping.ToleranceMultiplier > 0 ? mapping.ToleranceMultiplier : 1.0;

                    int? shownDecimals = mapping.Precision.Mode == PrecisionMode.MatchExpected
                        ? DisplayedPrecision.DecimalPlaces(sheet.GetDisplayText(row, expectedColumn))
                        : null;
                    double rounded = TolerancePrecision.Round(calc.Tolerance * factor, mapping.Precision, shownDecimals);
                    outcome.Calculated = rounded;

                    if (mode == DatasheetRunMode.Apply)
                    {
                        sheet.SetNumber(row, toleranceColumn, rounded);
                        outcome.Status = calc.Extrapolated ? RowStatus.Extrapolated : RowStatus.Written;
                        if (calc.Extrapolated)
                        {
                            outcome.Note = "Tolerance band extrapolates past the sensor range";
                        }

                        continue;
                    }

                    double? existing = sheet.GetNumber(row, toleranceColumn);
                    outcome.Existing = existing;
                    if (existing == null || !Close(existing.Value, rounded))
                    {
                        string factorNote = factor != 1.0 ? $" ×{factor:0.###}" : string.Empty;
                        outcome.Status = RowStatus.Mismatch;
                        outcome.Note =
                            $"{CommentMarker} test point {b + 1}: expected ± {rounded:0.######}{factorNote} (signal {signal.SensorName}, " +
                            $"{DescribeBand(calc)}); found {(existing.HasValue ? existing.Value.ToString("0.######") : "blank")}";
                        sheet.AddToolComment(row, toleranceColumn, outcome.Note);
                    }
                    else
                    {
                        outcome.Status = calc.Extrapolated ? RowStatus.Extrapolated : RowStatus.Matches;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Optional Pass/Fail pass: writes "Pass"/"Fail" into the Pass/Fail column from
        /// <c>|Actual − Expected| ≤ Tolerance</c>, using the values already in the sheet.
        /// Does not recalculate tolerances.
        /// </summary>
        public DatasheetRunResult RunPassFail(IDatasheet sheet, DatasheetMapping mapping)
        {
            var result = new DatasheetRunResult { Mode = DatasheetRunMode.Check };

            if (mapping.Orientation == DatasheetOrientation.ColumnPerCase)
            {
                sheet = new TransposedDatasheet(sheet);
            }

            string?[] headerRow = sheet.Row(mapping.HeaderRowIndex);
            var columns = new ColumnResolver(mapping, headerRow);
            foreach (ConfigIssue issue in columns.Issues)
            {
                result.SetupProblems.Add(issue.Message);
            }

            IReadOnlyList<int> expectedColumns = columns.Columns(DatasheetParameter.Expected);
            IReadOnlyList<int> actualColumns = columns.Columns(DatasheetParameter.Actual);
            IReadOnlyList<int> toleranceColumns = columns.Columns(DatasheetParameter.Tolerance);
            IReadOnlyList<int> passFailColumns = columns.Columns(DatasheetParameter.PassFail);
            int? systemIdColumn = columns.Column(DatasheetParameter.SystemId);

            int blocks = new[] { expectedColumns.Count, actualColumns.Count, toleranceColumns.Count, passFailColumns.Count }.Min();
            if (blocks == 0)
            {
                result.SetupProblems.Add("Pass/Fail needs the Expected, Actual, Tolerance and Pass/Fail headers mapped.");
            }

            if (!result.DidRun)
            {
                return result;
            }

            result.TestPointsPerRow = blocks;
            int firstRow = mapping.FirstDataRowIndex ?? mapping.HeaderRowIndex + 1;
            int lastRow = mapping.LastDataRowIndex ?? sheet.LastRowIndex;

            for (int row = firstRow; row <= lastRow; row++)
            {
                string systemId = systemIdColumn != null ? sheet.GetText(row, systemIdColumn.Value)?.Trim() ?? string.Empty : string.Empty;

                if (systemId.Length > 0 && _resolver.Resolve(systemId).Step == ResolutionStep.Excluded)
                {
                    result.Rows.Add(new RowOutcome { RowIndex = row, SystemId = systemId, Status = RowStatus.Skipped, Note = "Excluded by the resolution review" });
                    continue;
                }

                for (int b = 0; b < blocks; b++)
                {
                    double? expected = sheet.GetNumber(row, expectedColumns[b]);
                    double? actual = sheet.GetNumber(row, actualColumns[b]);
                    double? tolerance = sheet.GetNumber(row, toleranceColumns[b]);

                    if (expected == null && actual == null && tolerance == null)
                    {
                        continue;
                    }

                    var outcome = new RowOutcome { RowIndex = row, TestPoint = b + 1, SystemId = systemId };
                    result.Rows.Add(outcome);

                    if (expected == null || actual == null || tolerance == null)
                    {
                        outcome.Status = RowStatus.NotCalculable;
                        outcome.Note = "Expected, Actual and Tolerance must all be present";
                        continue;
                    }

                    bool pass = Math.Abs(actual.Value - expected.Value) <= Math.Abs(tolerance.Value) + 1e-12;
                    sheet.SetText(row, passFailColumns[b], pass ? "Pass" : "Fail");
                    outcome.Status = pass ? RowStatus.Matches : RowStatus.Mismatch;
                    outcome.Calculated = actual.Value - expected.Value;
                }
            }

            return result;
        }

        private static void CommentIfChecking(IDatasheet sheet, DatasheetRunMode mode, int row, int column, string note)
        {
            if (mode == DatasheetRunMode.Check)
            {
                sheet.AddToolComment(row, column, $"{CommentMarker} {note}");
            }
        }

        private static UnitSystem RowUnitSystem(IDatasheet sheet, int row, int? unitColumn, UnitSystem fallback)
        {
            if (unitColumn == null)
            {
                return fallback;
            }

            string? text = sheet.GetText(row, unitColumn.Value)?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return fallback;
            }

            string t = text!.ToLowerInvariant();
            if (t == "si" || t == "metric" || t == "s.i.")
            {
                return UnitSystem.Si;
            }

            if (t == "english" || t == "imperial" || t == "us")
            {
                return UnitSystem.English;
            }

            return fallback;
        }

        private static string DescribeBand(ToleranceResult calc) =>
            calc.UsedEuFastPath ? "EU terms" : $"raw ± {calc.RawTolerance:0.######}";

        private static bool Close(double a, double b)
        {
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            return Math.Abs(a - b) <= Math.Max(1e-9, 1e-6 * scale);
        }
    }
}
