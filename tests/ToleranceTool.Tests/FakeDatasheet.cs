using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ToleranceTool.Excel.Datasheet;

namespace ToleranceTool.Tests
{
    /// <summary>An in-memory <see cref="IDatasheet"/> for runner tests.</summary>
    public sealed class FakeDatasheet : IDatasheet
    {
        private readonly List<string?[]> _rows = new List<string?[]>();

        public FakeDatasheet(string name, IEnumerable<string?[]> rows)
        {
            Name = name;
            _rows.AddRange(rows);
        }

        public string Name { get; }

        public int LastRowIndex => _rows.Count - 1;

        public int LastColumnIndex => (_rows.Count == 0 ? 0 : _rows.Max(r => r.Length)) - 1;

        public IEnumerable<int> ColumnIndexes => Enumerable.Range(0, _rows.Count == 0 ? 0 : _rows.Max(r => r.Length));

        public List<(int Row, int Col, string Text)> Comments { get; } = new List<(int, int, string)>();

        public Dictionary<(int Row, int Col), double> Written { get; } = new Dictionary<(int, int), double>();

        /// <summary>Explicit display-text overrides keyed by (row, col); falls back to the raw string.</summary>
        public Dictionary<(int Row, int Col), string> Display { get; } = new Dictionary<(int, int), string>();

        public string?[] Row(int rowIndex) => rowIndex >= 0 && rowIndex < _rows.Count ? _rows[rowIndex] : Array.Empty<string?>();

        public string? GetText(int rowIndex, int columnIndex)
        {
            string?[] row = Row(rowIndex);
            string? value = columnIndex >= 0 && columnIndex < row.Length ? row[columnIndex] : null;
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public double? GetNumber(int rowIndex, int columnIndex)
        {
            if (Written.TryGetValue((rowIndex, columnIndex), out double w))
            {
                return w;
            }

            string? text = GetText(rowIndex, columnIndex);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : (double?)null;
        }

        public string? GetDisplayText(int rowIndex, int columnIndex) =>
            Display.TryGetValue((rowIndex, columnIndex), out string text) ? text : GetText(rowIndex, columnIndex);

        public void SetNumber(int rowIndex, int columnIndex, double value) => Written[(rowIndex, columnIndex)] = value;

        public void ClearToolComments(string markerPrefix) =>
            Comments.RemoveAll(c => c.Text.StartsWith(markerPrefix, StringComparison.Ordinal));

        public void AddToolComment(int rowIndex, int columnIndex, string text) => Comments.Add((rowIndex, columnIndex, text));
    }
}
