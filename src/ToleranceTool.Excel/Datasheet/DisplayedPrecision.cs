using System.Globalization;
using System.Linq;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>Reads how precisely a number is shown in a cell's displayed text.</summary>
    public static class DisplayedPrecision
    {
        /// <summary>
        /// The number of digits shown after the decimal point in
        /// <paramref name="displayText"/>, or null when it is not a plain number
        /// (blank, text, a date, "#DIV/0!" …). <c>"125"</c> → 0, <c>"125.0"</c> → 1,
        /// <c>"0.0480"</c> → 4.
        /// </summary>
        public static int? DecimalPlaces(string? displayText)
        {
            if (string.IsNullOrWhiteSpace(displayText))
            {
                return null;
            }

            string text = displayText!.Trim();
            text = text.TrimStart('+', '-', '$', '€', '£', ' ');
            text = text.Replace(",", string.Empty).TrimEnd('%', ' ');

            int fractionDigits = 0;
            int exponent = 0;

            int e = text.IndexOfAny(new[] { 'e', 'E' });
            if (e >= 0)
            {
                string exponentText = text.Substring(e + 1);
                if (!int.TryParse(exponentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent))
                {
                    return null;
                }

                text = text.Substring(0, e);
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return null;
            }

            int dot = text.IndexOf('.');
            if (dot >= 0)
            {
                fractionDigits = text.Substring(dot + 1).Count(char.IsDigit);
            }

            // A positive exponent shifts the point right (fewer shown decimals); a
            // negative one shifts it left (more).
            int places = fractionDigits - exponent;
            return places < 0 ? 0 : places;
        }
    }
}
