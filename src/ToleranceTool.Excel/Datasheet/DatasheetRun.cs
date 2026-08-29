using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Configuration.Datasheet;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.Excel.Datasheet
{
    public enum DatasheetRunMode
    {
        /// <summary>Write the calculated tolerance into the Tolerance column.</summary>
        Apply = 0,

        /// <summary>Calculate but write nothing; comment where the existing value disagrees.</summary>
        Check = 1,
    }

    public enum RowStatus
    {
        Written = 0,
        Matches = 1,
        Mismatch = 2,
        NoSignal = 3,
        AmbiguousSignal = 4,
        NoTolerance = 5,
        NotCalculable = 6,
        Extrapolated = 7,
        Skipped = 8,
    }

    public sealed class RowOutcome
    {
        public int RowIndex { get; set; }

        /// <summary>1-based test-point number within the row (1 for a single-block datasheet).</summary>
        public int TestPoint { get; set; } = 1;

        public string SystemId { get; set; } = string.Empty;

        public ResolutionStep Resolution { get; set; }

        public RowStatus Status { get; set; }

        public double? Calculated { get; set; }

        public double? Existing { get; set; }

        public string? Note { get; set; }
    }

    public sealed class DatasheetRunResult
    {
        public DatasheetRunMode Mode { get; set; }

        public List<RowOutcome> Rows { get; } = new List<RowOutcome>();

        public int Considered => Rows.Count;

        public int Written => Rows.Count(r => r.Status == RowStatus.Written);

        public int Matches => Rows.Count(r => r.Status == RowStatus.Matches);

        public int Mismatched => Rows.Count(r => r.Status == RowStatus.Mismatch);

        public int Extrapolated => Rows.Count(r => r.Status == RowStatus.Extrapolated);

        public int Uncheckable => Rows.Count(r =>
            r.Status == RowStatus.NoSignal ||
            r.Status == RowStatus.AmbiguousSignal ||
            r.Status == RowStatus.NoTolerance ||
            r.Status == RowStatus.NotCalculable);

        /// <summary>Rows the user marked to be left alone.</summary>
        public int Excluded => Rows.Count(r => r.Status == RowStatus.Skipped);

        /// <summary>Set when the run could not start (bad mapping, missing columns).</summary>
        public List<string> SetupProblems { get; } = new List<string>();

        /// <summary>Non-fatal notes (e.g. an uneven number of Expected vs. Tolerance columns).</summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Number of test-point column groups the run used per row.</summary>
        public int TestPointsPerRow { get; set; } = 1;

        /// <summary>The tolerance multiplier that was applied (1 for accuracy testing).</summary>
        public double ToleranceMultiplier { get; set; } = 1.0;

        public bool DidRun => SetupProblems.Count == 0;

        public string Summary()
        {
            if (!DidRun)
            {
                return "Could not run: " + string.Join("; ", SetupProblems);
            }

            string points = TestPointsPerRow > 1 ? $" across {TestPointsPerRow} test points/row" : string.Empty;
            string excluded = Excluded > 0 ? $", {Excluded} excluded" : string.Empty;
            string factor = System.Math.Abs(ToleranceMultiplier - 1.0) > 1e-9 ? $"  [tolerance ×{ToleranceMultiplier:0.###}]" : string.Empty;
            string warn = Warnings.Count > 0 ? "  (" + string.Join("; ", Warnings) + ")" : string.Empty;

            return (Mode == DatasheetRunMode.Apply
                ? $"{Written} written, {Extrapolated} extrapolated, {Uncheckable} not calculable{excluded}{points}."
                : $"{Considered} checked, {Mismatched} mismatched, {Uncheckable} un-checkable{excluded}{points}.") + factor + warn;
        }
    }
}
