using System;

namespace ToleranceTool.Core.Precision
{
    /// <summary>
    /// Rounds a calculated tolerance according to a <see cref="PrecisionPolicy"/>.
    /// Kept out of the engine: the engine produces the exact value, and the write
    /// path rounds it once the Expected cell's displayed precision is known.
    /// </summary>
    public static class TolerancePrecision
    {
        /// <summary>
        /// Rounds <paramref name="tolerance"/> per <paramref name="policy"/>.
        /// For <see cref="PrecisionMode.MatchExpected"/>, pass the number of decimal
        /// places shown in the Expected cell; when it is null the value is returned
        /// unrounded (the caller could not determine the displayed precision).
        /// </summary>
        public static double Round(double tolerance, PrecisionPolicy policy, int? expectedDecimalPlaces = null)
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            if (!IsFinite(tolerance))
            {
                return tolerance;
            }

            switch (policy.Mode)
            {
                case PrecisionMode.MatchExpected:
                    return expectedDecimalPlaces.HasValue
                        ? RoundToDecimalPlaces(tolerance, Math.Min(expectedDecimalPlaces.Value, 15), policy.Rounding)
                        : tolerance;

                case PrecisionMode.SignificantFigures:
                    return RoundToSignificantFigures(tolerance, policy.Digits, policy.Rounding);

                case PrecisionMode.DecimalPlaces:
                    return RoundToDecimalPlaces(tolerance, policy.Digits, policy.Rounding);

                default:
                    return tolerance;
            }
        }

        public static double RoundToSignificantFigures(double value, int figures, RoundingMode rounding)
        {
            if (figures < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(figures), "At least one significant figure is required.");
            }

            if (value == 0 || !IsFinite(value))
            {
                return value;
            }

            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            int decimals = figures - 1 - exponent;

            // Very large or very small magnitudes push the decimal count past what
            // Math.Round accepts; scale the value instead.
            if (decimals >= 0 && decimals <= 15)
            {
                return RoundToDecimalPlaces(value, decimals, rounding);
            }

            double scale = Math.Pow(10, decimals);
            return RoundToDecimalPlaces(value * scale, 0, rounding) / scale;
        }

        public static double RoundToDecimalPlaces(double value, int places, RoundingMode rounding)
        {
            if (places < 0 || places > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(places), "Decimal places must be between 0 and 15.");
            }

            if (!IsFinite(value))
            {
                return value;
            }

            MidpointRounding midpoint = rounding == RoundingMode.HalfUp
                ? MidpointRounding.AwayFromZero
                : MidpointRounding.ToEven;

            return Math.Round(value, places, midpoint);
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
