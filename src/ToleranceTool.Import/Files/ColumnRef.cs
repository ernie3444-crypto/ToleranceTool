using System;

namespace ToleranceTool.Import.Files
{
    /// <summary>Converts between Excel column letters (<c>A</c>, <c>W</c>, <c>AA</c>) and zero-based indexes.</summary>
    public static class ColumnRef
    {
        public static int ToIndex(string columnLetter)
        {
            if (string.IsNullOrWhiteSpace(columnLetter))
            {
                throw new ArgumentException("A column letter is required.", nameof(columnLetter));
            }

            string trimmed = columnLetter.Trim().ToUpperInvariant();
            int index = 0;
            foreach (char c in trimmed)
            {
                if (c < 'A' || c > 'Z')
                {
                    throw new FormatException($"\"{columnLetter}\" is not a column letter.");
                }

                index = index * 26 + (c - 'A' + 1);
            }

            return index - 1;
        }

        public static string FromIndex(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            string result = string.Empty;
            int n = index + 1;
            while (n > 0)
            {
                int remainder = (n - 1) % 26;
                result = (char)('A' + remainder) + result;
                n = (n - 1) / 26;
            }

            return result;
        }

        /// <summary>Accepts a column letter or a 1-based number; returns the zero-based index.</summary>
        public static bool TryParse(string? reference, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(reference))
            {
                return false;
            }

            string trimmed = reference!.Trim();
            if (int.TryParse(trimmed, out int oneBased))
            {
                if (oneBased < 1)
                {
                    return false;
                }

                index = oneBased - 1;
                return true;
            }

            try
            {
                index = ToIndex(trimmed);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
