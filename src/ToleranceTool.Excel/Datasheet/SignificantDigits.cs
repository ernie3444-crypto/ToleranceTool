using System.Globalization;
using System.Linq;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>Counts the significant digits shown in a cell's displayed text.</summary>
    public static class SignificantDigits
    {
        /// <summary>
        /// Returns the significant-digit count of <paramref name="displayText"/>, or
        /// null when it is not a plain number (blank, text, a date, "#DIV/0!" …).
        /// </summary>
        public static int? Count(string? displayText)
        {
            if (string.IsNullOrWhiteSpace(displayText))
            {
                return null;
            }

            string text = displayText!.Trim();

            // Strip a leading sign, thousands separators, a percent sign, and currency symbols.
            text = text.TrimStart('+', '-', '$', '€', '£', ' ');
            text = text.Replace(",", string.Empty).TrimEnd('%', ' ');

            // Scientific notation: significand carries the significant digits.
            int e = text.IndexOfAny(new[] { 'e', 'E' });
            if (e >= 0)
            {
                text = text.Substring(0, e);
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return null;
            }

            string digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                return null;
            }

            bool hasFraction = text.Contains('.');

            if (hasFraction)
            {
                // Leading zeros are never significant; trailing zeros after a point are.
                string trimmedLeading = digits.TrimStart('0');
                return trimmedLeading.Length == 0 ? 1 : trimmedLeading.Length;
            }

            // Integer display: drop leading zeros; trailing zeros are ambiguous — count them
            // as significant, which matches "match what is shown".
            string integer = digits.TrimStart('0');
            return integer.Length == 0 ? 1 : integer.Length;
        }
    }
}
