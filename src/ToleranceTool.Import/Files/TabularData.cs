using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExcelDataReader;

namespace ToleranceTool.Import.Files
{
    /// <summary>
    /// A file read into a rectangular grid of string cells. Row-oriented sources
    /// (CSV, a normal sheet) and the header handling are the same downstream.
    /// </summary>
    public sealed class TabularData
    {
        private readonly List<string?[]> _rows;

        public TabularData(List<string?[]> rows)
        {
            _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        public int RowCount => _rows.Count;

        public IReadOnlyList<string?[]> Rows => _rows;

        /// <summary>The cell at (<paramref name="row"/>, <paramref name="column"/>), or null when out of range.</summary>
        public string? Cell(int row, int column)
        {
            if (row < 0 || row >= _rows.Count)
            {
                return null;
            }

            string?[] cells = _rows[row];
            return column >= 0 && column < cells.Length ? cells[column] : null;
        }

        public static TabularData Read(string path, string? sheetName = null)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".csv":
                case ".tsv":
                case ".txt":
                    return ParseCsv(File.ReadAllText(path), extension == ".tsv" ? '\t' : ',');

                case ".xlsx":
                case ".xls":
                case ".xlsb":
                    return ReadWorkbook(path, sheetName);

                default:
                    throw new NotSupportedException($"Cannot read \"{extension}\" files as a signal source.");
            }
        }

        // --- CSV ------------------------------------------------------------

        public static TabularData ParseCsv(string text, char delimiter = ',')
        {
            var rows = new List<string?[]>();
            var row = new List<string?>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool fieldStarted = false;

            void EndField()
            {
                row.Add(fieldStarted || field.Length > 0 ? field.ToString() : null);
                field.Clear();
                fieldStarted = false;
            }

            void EndRow()
            {
                EndField();
                rows.Add(row.ToArray());
                row.Clear();
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        fieldStarted = true;
                        break;

                    case '\r':
                        break;

                    case '\n':
                        EndRow();
                        break;

                    default:
                        if (c == delimiter)
                        {
                            EndField();
                        }
                        else
                        {
                            field.Append(c);
                        }

                        break;
                }
            }

            if (field.Length > 0 || fieldStarted || row.Count > 0)
            {
                EndRow();
            }

            return new TabularData(rows);
        }

        // --- workbook -----------------------------------------------------

        private static TabularData ReadWorkbook(string path, string? sheetName)
        {
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                do
                {
                    if (sheetName != null && !string.Equals(reader.Name, sheetName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var rows = new List<string?[]>();
                    while (reader.Read())
                    {
                        var cells = new string?[reader.FieldCount];
                        for (int c = 0; c < reader.FieldCount; c++)
                        {
                            object value = reader.GetValue(c);
                            cells[c] = value?.ToString();
                        }

                        rows.Add(cells);
                    }

                    return new TabularData(rows);
                }
                while (reader.NextResult());
            }

            throw new ArgumentException(
                sheetName == null ? "The workbook has no sheets." : $"The workbook has no sheet named \"{sheetName}\".");
        }
    }
}
