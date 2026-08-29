using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>
    /// <see cref="IDatasheet"/> over a live Excel worksheet COM object. All Excel
    /// coordinates are 1-based; this class exposes the zero-based view the runner uses.
    /// </summary>
    public sealed class ExcelDatasheet : IDatasheet
    {
        private readonly dynamic _worksheet;
        private readonly int _firstRow;
        private readonly int _firstColumn;
        private readonly int _lastRow;
        private readonly int _lastColumn;

        public ExcelDatasheet(dynamic worksheet)
        {
            _worksheet = worksheet;

            dynamic used = worksheet.UsedRange;
            _firstRow = (int)used.Row;
            _firstColumn = (int)used.Column;
            _lastRow = _firstRow + (int)used.Rows.Count - 1;
            _lastColumn = _firstColumn + (int)used.Columns.Count - 1;
        }

        public string Name => (string)_worksheet.Name;

        public int LastRowIndex => _lastRow - 1;

        public int LastColumnIndex => _lastColumn - 1;

        public IEnumerable<int> ColumnIndexes => Enumerable.Range(0, _lastColumn);

        public string?[] Row(int rowIndex)
        {
            var cells = new string?[_lastColumn];
            for (int c = 0; c < _lastColumn; c++)
            {
                cells[c] = GetText(rowIndex, c);
            }

            return cells;
        }

        public string? GetText(int rowIndex, int columnIndex)
        {
            object? value = RawValue(rowIndex, columnIndex);
            if (value == null)
            {
                return null;
            }

            if (value is double d)
            {
                return d.ToString("R", CultureInfo.InvariantCulture);
            }

            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return text.Length == 0 ? null : text;
        }

        public double? GetNumber(int rowIndex, int columnIndex)
        {
            object? value = RawValue(rowIndex, columnIndex);
            switch (value)
            {
                case null:
                    return null;
                case double d:
                    return d;
                case bool _:
                    return null;
                default:
                    return double.TryParse(
                        Convert.ToString(value, CultureInfo.InvariantCulture),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double parsed)
                        ? parsed
                        : (double?)null;
            }
        }

        public string? GetDisplayText(int rowIndex, int columnIndex)
        {
            try
            {
                dynamic cell = Cell(rowIndex, columnIndex);
                var text = (string?)cell.Text;
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch
            {
                return GetText(rowIndex, columnIndex);
            }
        }

        public void SetNumber(int rowIndex, int columnIndex, double value)
        {
            Cell(rowIndex, columnIndex).Value2 = value;
        }

        public void ClearToolComments(string markerPrefix)
        {
            dynamic comments = _worksheet.Comments;
            int count = (int)comments.Count;
            for (int i = count; i >= 1; i--)
            {
                dynamic comment = comments[i];
                string text = SafeCommentText(comment);
                if (text.StartsWith(markerPrefix, StringComparison.Ordinal))
                {
                    comment.Delete();
                }
            }
        }

        public void AddToolComment(int rowIndex, int columnIndex, string text)
        {
            dynamic cell = Cell(rowIndex, columnIndex);
            try
            {
                if (cell.Comment != null)
                {
                    cell.Comment.Delete();
                }
            }
            catch
            {
                // no existing comment
            }

            dynamic comment = cell.AddComment(text);
            comment.Shape.TextFrame.AutoSize = true;
        }

        private object? RawValue(int rowIndex, int columnIndex)
        {
            object value = Cell(rowIndex, columnIndex).Value2;
            if (value == null)
            {
                return null;
            }

            if (value is string s && s.Length == 0)
            {
                return null;
            }

            return value;
        }

        private dynamic Cell(int rowIndex, int columnIndex) =>
            _worksheet.Cells[rowIndex + 1, columnIndex + 1];

        private static string SafeCommentText(dynamic comment)
        {
            try
            {
                return (string)comment.Text() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
