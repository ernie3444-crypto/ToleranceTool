namespace ToleranceTool.Core.Precision
{
    public enum PrecisionMode
    {
        /// <summary>
        /// Round to the number of significant digits shown in the row's Expected
        /// cell. The digit count is read from Excel and passed to the rounder.
        /// </summary>
        MatchExpected = 0,

        /// <summary>Round to a fixed number of significant figures.</summary>
        SignificantFigures = 1,

        /// <summary>Round to a fixed number of decimal places.</summary>
        DecimalPlaces = 2,
    }

    public enum RoundingMode
    {
        /// <summary>0.5 rounds away from zero.</summary>
        HalfUp = 0,

        /// <summary>0.5 rounds to the nearest even digit (banker's rounding).</summary>
        HalfToEven = 1,
    }

    /// <summary>
    /// How a calculated tolerance is rounded before it is written to the sheet.
    /// Stored per worksheet as part of the datasheet mapping.
    /// </summary>
    public sealed class PrecisionPolicy
    {
        public PrecisionMode Mode { get; set; } = PrecisionMode.MatchExpected;

        /// <summary>Significant figures or decimal places for the fixed modes.</summary>
        public int Digits { get; set; } = 3;

        public RoundingMode Rounding { get; set; } = RoundingMode.HalfToEven;

        public static PrecisionPolicy MatchExpected(RoundingMode rounding = RoundingMode.HalfToEven) =>
            new PrecisionPolicy { Mode = PrecisionMode.MatchExpected, Rounding = rounding };

        public static PrecisionPolicy SignificantFigures(int digits, RoundingMode rounding = RoundingMode.HalfToEven) =>
            new PrecisionPolicy { Mode = PrecisionMode.SignificantFigures, Digits = digits, Rounding = rounding };

        public static PrecisionPolicy DecimalPlaces(int digits, RoundingMode rounding = RoundingMode.HalfToEven) =>
            new PrecisionPolicy { Mode = PrecisionMode.DecimalPlaces, Digits = digits, Rounding = rounding };
    }
}
