using System.Collections.Generic;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>
    /// The worksheet operations the tolerance run needs, abstracted so the run
    /// logic can be tested without Excel. All indexes are zero-based.
    /// </summary>
    public interface IDatasheet
    {
        string Name { get; }

        /// <summary>Zero-based index of the last row that holds data.</summary>
        int LastRowIndex { get; }

        /// <summary>Zero-based index of the last column that holds data.</summary>
        int LastColumnIndex { get; }

        /// <summary>The cells of one row as strings (trimmed by the caller). Missing cells are null.</summary>
        string?[] Row(int rowIndex);

        /// <summary>The cell's underlying value as a string, or null when empty.</summary>
        string? GetText(int rowIndex, int columnIndex);

        /// <summary>The cell's value as a number, or null when it is blank or non-numeric.</summary>
        double? GetNumber(int rowIndex, int columnIndex);

        /// <summary>The cell's displayed text, honouring its number format — used to count shown significant digits.</summary>
        string? GetDisplayText(int rowIndex, int columnIndex);

        void SetNumber(int rowIndex, int columnIndex, double value);

        void SetText(int rowIndex, int columnIndex, string value);

        /// <summary>Removes every classic note whose text starts with <paramref name="markerPrefix"/>.</summary>
        void ClearToolComments(string markerPrefix);

        /// <summary>Adds a classic note carrying the marker prefix.</summary>
        void AddToolComment(int rowIndex, int columnIndex, string text);

        IEnumerable<int> ColumnIndexes { get; }
    }
}
