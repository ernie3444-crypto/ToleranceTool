using System;
using System.Collections.Generic;
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
            var result = new DatasheetRunResult { Mode = mode };

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
            int expectedColumn = columns.Require(DatasheetParameter.Expected);
            int toleranceColumn = columns.Require(DatasheetParameter.Tolerance);
            int? unitColumn = columns.UnitColumnIndex;

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

                var outcome = new RowOutcome { RowIndex = row, SystemId = systemId! };
                result.Rows.Add(outcome);

                SignalResolution resolution = _resolver.Resolve(systemId!);
                outcome.Resolution = resolution.Step;

                if (!resolution.IsResolved)
                {
                    outcome.Status = resolution.Step == ResolutionStep.Ambiguous ? RowStatus.AmbiguousSignal : RowStatus.NoSignal;
                    outcome.Note = resolution.Step == ResolutionStep.Ambiguous
                        ? $"System ID matches several signals: {string.Join(", ", resolution.Candidates)}"
                        : "System ID did not resolve to a signal";
                    CommentIfChecking(sheet, mode, row, toleranceColumn, outcome.Note);
                    continue;
                }

                SignalConfig signal = resolution.Signal!;

                if (!_tolerances.TryGet(signal.SignalType, signal.ModuleType, out ToleranceDefinition tolerance))
                {
                    outcome.Status = RowStatus.NoTolerance;
                    outcome.Note = $"No tolerance for {signal.SignalType} / {signal.ModuleType}";
                    CommentIfChecking(sheet, mode, row, toleranceColumn, outcome.Note);
                    continue;
                }

                double? expected = sheet.GetNumber(row, expectedColumn);
                if (expected == null)
                {
                    outcome.Status = RowStatus.NotCalculable;
                    outcome.Note = "Expected value is blank or not a number";
                    CommentIfChecking(sheet, mode, row, toleranceColumn, outcome.Note);
                    continue;
                }

                UnitSystem unitSystem = RowUnitSystem(sheet, row, unitColumn, mapping.DefaultUnitSystem);
                ToleranceResult calc = _engine.Calculate(expected.Value, unitSystem, signal, tolerance);

                if (!calc.IsCalculated)
                {
                    outcome.Status = RowStatus.NotCalculable;
                    outcome.Note = calc.Message ?? calc.Outcome.ToString();
                    CommentIfChecking(sheet, mode, row, toleranceColumn, outcome.Note);
                    continue;
                }

                int? shownDigits = mapping.Precision.Mode == PrecisionMode.MatchExpected
                    ? SignificantDigits.Count(sheet.GetDisplayText(row, expectedColumn))
                    : null;
                double rounded = TolerancePrecision.Round(calc.Tolerance, mapping.Precision, shownDigits);
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
                    outcome.Status = RowStatus.Mismatch;
                    outcome.Note =
                        $"{CommentMarker} expected ± {rounded:0.######} (signal {signal.SensorName}, " +
                        $"{DescribeBand(calc)}); found {(existing.HasValue ? existing.Value.ToString("0.######") : "blank")}";
                    sheet.AddToolComment(row, toleranceColumn, outcome.Note);
                }
                else
                {
                    outcome.Status = calc.Extrapolated ? RowStatus.Extrapolated : RowStatus.Matches;
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

            int? expectedColumn = columns.Column(DatasheetParameter.Expected);
            int? actualColumn = columns.Column(DatasheetParameter.Actual);
            int? toleranceColumn = columns.Column(DatasheetParameter.Tolerance);
            int? passFailColumn = columns.Column(DatasheetParameter.PassFail);
            int? systemIdColumn = columns.Column(DatasheetParameter.SystemId);

            if (expectedColumn == null || actualColumn == null || toleranceColumn == null || passFailColumn == null)
            {
                result.SetupProblems.Add("Pass/Fail needs the Expected, Actual, Tolerance and Pass/Fail headers mapped.");
            }

            if (!result.DidRun)
            {
                return result;
            }

            int firstRow = mapping.FirstDataRowIndex ?? mapping.HeaderRowIndex + 1;
            int lastRow = mapping.LastDataRowIndex ?? sheet.LastRowIndex;

            for (int row = firstRow; row <= lastRow; row++)
            {
                double? expected = sheet.GetNumber(row, expectedColumn!.Value);
                double? actual = sheet.GetNumber(row, actualColumn!.Value);
                double? tolerance = sheet.GetNumber(row, toleranceColumn!.Value);

                if (expected == null && actual == null && tolerance == null)
                {
                    continue;
                }

                var outcome = new RowOutcome
                {
                    RowIndex = row,
                    SystemId = systemIdColumn != null ? sheet.GetText(row, systemIdColumn.Value)?.Trim() ?? string.Empty : string.Empty,
                };
                result.Rows.Add(outcome);

                if (expected == null || actual == null || tolerance == null)
                {
                    outcome.Status = RowStatus.NotCalculable;
                    outcome.Note = "Expected, Actual and Tolerance must all be present";
                    continue;
                }

                bool pass = Math.Abs(actual.Value - expected.Value) <= Math.Abs(tolerance.Value) + 1e-12;
                sheet.SetText(row, passFailColumn!.Value, pass ? "Pass" : "Fail");
                outcome.Status = pass ? RowStatus.Matches : RowStatus.Mismatch;
                outcome.Calculated = actual.Value - expected.Value;
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
