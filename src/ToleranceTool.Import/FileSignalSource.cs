using System;
using System.Collections.Generic;
using ToleranceTool.Import.Files;

namespace ToleranceTool.Import
{
    /// <summary>
    /// An <see cref="ISignalSource"/> backed by a delimited-text file or a workbook
    /// sheet, read closed. Row-oriented at launch (column orientation is P7).
    /// </summary>
    public sealed class FileSignalSource : ISignalSource
    {
        private readonly ImportSourceDefinition _definition;
        private readonly TabularData _data;

        private readonly bool _labelRowIsFirst;

        public FileSignalSource(ImportSourceDefinition definition, TabularData data)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // A column-oriented source (one signal per column, field labels down a
            // column) is read by transposing it: each original column becomes a row,
            // and the field locators — which were row numbers — become column refs.
            if (_definition.Orientation == SignalDataOrientation.ColumnPerSignal)
            {
                _data = data.Transpose();
                _labelRowIsFirst = true;
            }
            else
            {
                _data = data;
            }
        }

        public static FileSignalSource Open(ImportSourceDefinition definition) =>
            new FileSignalSource(definition, TabularData.Read(definition.Location, definition.SheetName));

        public string Name => _definition.Name;

        public bool IsMaster => _definition.IsMaster;

        public IReadOnlyList<SignalFieldRecord> Read()
        {
            if (!ColumnRef.TryParse(_definition.UniversalIdLocator, out int keyColumn))
            {
                throw new FormatException(
                    $"Source \"{Name}\": \"{_definition.UniversalIdLocator}\" is not a column reference.");
            }

            var bindings = new List<(int Column, FieldBinding Binding)>();
            foreach (FieldBinding binding in _definition.Fields)
            {
                if (!ColumnRef.TryParse(binding.Locator, out int column))
                {
                    throw new FormatException(
                        $"Source \"{Name}\", field {binding.Field}: \"{binding.Locator}\" is not a column reference.");
                }

                bindings.Add((column, binding));
            }

            int firstDataRow = _labelRowIsFirst
                ? 1
                : _definition.HeaderRowIndex.HasValue ? _definition.HeaderRowIndex.Value + 1 : 0;
            var records = new List<SignalFieldRecord>();

            for (int row = firstDataRow; row < _data.RowCount; row++)
            {
                string? key = Trim(_data.Cell(row, keyColumn));
                if (key == null)
                {
                    continue; // a blank key row is skipped, not an error
                }

                var record = new SignalFieldRecord { UniversalId = key };
                foreach ((int column, FieldBinding binding) in bindings)
                {
                    record.Fields[binding.Field] = Trim(_data.Cell(row, column));
                }

                records.Add(record);
            }

            return records;
        }

        private static string? Trim(string? value)
        {
            if (value == null)
            {
                return null;
            }

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
