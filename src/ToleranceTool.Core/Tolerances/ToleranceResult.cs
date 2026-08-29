using ToleranceTool.Core.Signals;

namespace ToleranceTool.Core.Tolerances
{
    public enum ToleranceOutcome
    {
        /// <summary>A tolerance was calculated.</summary>
        Calculated = 0,

        /// <summary>The datasheet row's System ID did not resolve to a signal.</summary>
        NoSignalMatch = 1,

        /// <summary>The System ID resolved to more than one signal.</summary>
        AmbiguousSignalMatch = 2,

        /// <summary>No tolerance definition for the signal's type + module.</summary>
        NoToleranceMatch = 3,

        /// <summary>The band ran outside the raw range where the scale curve is undefined.</summary>
        CurveUndefined = 4,

        /// <summary>The expected value was missing or not a number.</summary>
        InvalidExpected = 5,
    }

    /// <summary>
    /// The output of one tolerance calculation, including every intermediate so a
    /// Check run or an audit view can show the working.
    /// </summary>
    public sealed class ToleranceResult
    {
        public ToleranceOutcome Outcome { get; set; } = ToleranceOutcome.Calculated;

        public string? Message { get; set; }

        public double Expected { get; set; }

        public UnitSystem UnitSystem { get; set; } = UnitSystem.English;

        /// <summary>The calculated ± tolerance in the row's EU units. Valid when Outcome is Calculated.</summary>
        public double Tolerance { get; set; }

        /// <summary>True when the EU fast path was used (no scale round-trip).</summary>
        public bool UsedEuFastPath { get; set; }

        // --- round-trip intermediates (Path B) ---
        public double RawExpected { get; set; }
        public double RawTolerance { get; set; }
        public double RawPlus { get; set; }
        public double RawMinus { get; set; }
        public double EuPlus { get; set; }
        public double EuMinus { get; set; }

        /// <summary>True when a raw edge fell outside [RawLow, RawHigh] and the curve was extrapolated.</summary>
        public bool Extrapolated { get; set; }

        public bool IsCalculated => Outcome == ToleranceOutcome.Calculated;

        public static ToleranceResult Failure(ToleranceOutcome outcome, string message) => new ToleranceResult
        {
            Outcome = outcome,
            Message = message,
        };
    }
}
