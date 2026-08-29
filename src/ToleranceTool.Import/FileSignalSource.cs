using System;
using System.Collections.Generic;
using ToleranceTool.Import.Files;

namespace ToleranceTool.Import
{
    /// <summary>
    /// An <see cref="ISignalSource"/> backed by a delimited-text file or a workbook
    /// sheet, read closed. Handles all three layouts: one signal per row, one signal
    /// per column, and one parameter per row.
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

        public IReadOnlyList<SignalFieldRecord> Read() =>
            _definition.Orientation == SignalDataOrientation.ParameterPerRow
                ? ReadParameterPerRow()
                : ReadTabular();

        // --- one signal per row (or per column, after the transpose) ---------

        private IReadOnlyList<SignalFieldRecord> ReadTabular()
        {
            int keyColumn = RequireColumn(_definition.UniversalIdLocator, "the Universal ID");

            var bindings = new List<(int Column, FieldBinding Binding)>();
            foreach (FieldBinding binding in _definition.Fields)
            {
                bindings.Add((RequireColumn(binding.Locator, $"field {binding.Field}"), binding));
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
                    continue;
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

        // --- one parameter per row ------------------------------------------

        private IReadOnlyList<SignalFieldRecord> ReadParameterPerRow()
        {
            int keyColumn = RequireColumn(_definition.UniversalIdLocator, "the Universal ID");
            int nameColumn = RequireColumn(_definition.ParameterNameLocator, "the parameter-name column");
            int valueColumn = RequireColumn(_definition.ParameterValueLocator, "the parameter-value column");
            int metricColumn = -1;
            if (!string.IsNullOrWhiteSpace(_definition.ParameterMetricLocator))
            {
                metricColumn = RequireColumn(_definition.ParameterMetricLocator, "the metric-value column");
            }

            // FieldBinding.Locator holds the parameter-name text that maps to the field.
            var nameToField = new Dictionary<string, FieldBinding>(StringComparer.OrdinalIgnoreCase);
            foreach (FieldBinding binding in _definition.Fields)
            {
                if (!string.IsNullOrWhiteSpace(binding.Locator))
                {
                    nameToField[binding.Locator.Trim()] = binding;
                }
            }

            int firstDataRow = _definition.HeaderRowIndex.HasValue ? _definition.HeaderRowIndex.Value + 1 : 0;

            var byKey = new Dictionary<string, SignalFieldRecord>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            for (int row = firstDataRow; row < _data.RowCount; row++)
            {
                string? key = Trim(_data.Cell(row, keyColumn));
                string? parameterName = Trim(_data.Cell(row, nameColumn));
                if (key == null || parameterName == null)
                {
                    continue;
                }

                if (!nameToField.TryGetValue(parameterName, out FieldBinding binding))
                {
                    continue; // a parameter the user did not map — ignored
                }

                if (!byKey.TryGetValue(key, out SignalFieldRecord record))
                {
                    record = new SignalFieldRecord { UniversalId = key };
                    byKey[key] = record;
                    order.Add(key);
                }

                string? value = Trim(_data.Cell(row, valueColumn));
                if (value != null)
                {
                    record.Fields[binding.Field] = value;
                }

                if (metricColumn >= 0)
                {
                    string? metric = Trim(_data.Cell(row, metricColumn));
                    string? siField = SignalField.SiCounterpart(binding.Field);
                    if (metric != null && siField != null)
                    {
                        record.Fields[siField] = metric;
                    }
                }
            }

            var records = new List<SignalFieldRecord>(order.Count);
            foreach (string key in order)
            {
                records.Add(byKey[key]);
            }

            return records;
        }

        private int RequireColumn(string? locator, string what)
        {
            if (!ColumnRef.TryParse(locator, out int column))
            {
                throw new FormatException($"Source \"{Name}\": \"{locator}\" is not a column reference for {what}.");
            }

            return column;
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
