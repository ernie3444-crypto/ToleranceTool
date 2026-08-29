using System.Collections.Generic;
using System.Linq;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>
    /// Swaps rows and columns of an <see cref="IDatasheet"/>. A column-oriented
    /// datasheet (one test case per column, parameter labels down a column) becomes
    /// row-oriented through this decorator, so the runner needs no special case.
    /// </summary>
    public sealed class TransposedDatasheet : IDatasheet
    {
        private readonly IDatasheet _inner;

        public TransposedDatasheet(IDatasheet inner)
        {
            _inner = inner;
        }

        public string Name => _inner.Name;

        public int LastRowIndex => _inner.LastColumnIndex;

        public int LastColumnIndex => _inner.LastRowIndex;

        public IEnumerable<int> ColumnIndexes => Enumerable.Range(0, _inner.LastRowIndex + 1);

        public string?[] Row(int rowIndex)
        {
            var cells = new string?[_inner.LastRowIndex + 1];
            for (int c = 0; c <= _inner.LastRowIndex; c++)
            {
                cells[c] = _inner.GetText(c, rowIndex);
            }

            return cells;
        }

        public string? GetText(int rowIndex, int columnIndex) => _inner.GetText(columnIndex, rowIndex);

        public double? GetNumber(int rowIndex, int columnIndex) => _inner.GetNumber(columnIndex, rowIndex);

        public string? GetDisplayText(int rowIndex, int columnIndex) => _inner.GetDisplayText(columnIndex, rowIndex);

        public void SetNumber(int rowIndex, int columnIndex, double value) => _inner.SetNumber(columnIndex, rowIndex, value);

        public void SetText(int rowIndex, int columnIndex, string value) => _inner.SetText(columnIndex, rowIndex, value);

        public void ClearToolComments(string markerPrefix) => _inner.ClearToolComments(markerPrefix);

        public void AddToolComment(int rowIndex, int columnIndex, string text) => _inner.AddToolComment(columnIndex, rowIndex, text);
    }
}
