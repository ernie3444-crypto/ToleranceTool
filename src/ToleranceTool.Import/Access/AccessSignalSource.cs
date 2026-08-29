using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Globalization;

namespace ToleranceTool.Import.Access
{
    /// <summary>Minimal row reader — the part of a DB result set the mapper needs. Lets the mapping be tested without a live DB.</summary>
    public interface IRecordReader
    {
        IReadOnlyList<string> Columns { get; }

        bool Read();

        object? Value(string column);
    }

    /// <summary>
    /// An <see cref="ISignalSource"/> over an Access query. The field bindings'
    /// <see cref="FieldBinding.Locator"/> and <see cref="ImportSourceDefinition.UniversalIdLocator"/>
    /// name result-set columns. Reading uses OleDb + the ACE provider; the join and
    /// completeness validation are the same pipeline as the file sources.
    /// </summary>
    public sealed class AccessSignalSource : ISignalSource
    {
        private readonly ImportSourceDefinition _definition;
        private readonly Func<IRecordReader> _openReader;

        public AccessSignalSource(ImportSourceDefinition definition, Func<IRecordReader>? openReader = null)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));

            if (_definition.Kind != SignalSourceKind.Access)
            {
                throw new ArgumentException("The source definition is not an Access source.", nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(_definition.Query))
            {
                throw new ArgumentException("An Access source needs a query.", nameof(definition));
            }

            _openReader = openReader ?? OpenOleDbReader;
        }

        public string Name => _definition.Name;

        public bool IsMaster => _definition.IsMaster;

        public IReadOnlyList<SignalFieldRecord> Read()
        {
            IRecordReader reader = _openReader();
            try
            {
                return Map(reader, _definition);
            }
            finally
            {
                (reader as IDisposable)?.Dispose();
            }
        }

        /// <summary>Maps a result set to field records. The Universal ID column must be non-blank on every row.</summary>
        public static IReadOnlyList<SignalFieldRecord> Map(IRecordReader reader, ImportSourceDefinition definition)
        {
            var records = new List<SignalFieldRecord>();

            while (reader.Read())
            {
                string? universalId = Text(reader.Value(definition.UniversalIdLocator));
                if (universalId == null)
                {
                    continue;
                }

                var record = new SignalFieldRecord { UniversalId = universalId };
                foreach (FieldBinding binding in definition.Fields)
                {
                    if (!string.IsNullOrWhiteSpace(binding.Locator))
                    {
                        record.Fields[binding.Field] = Text(reader.Value(binding.Locator));
                    }
                }

                records.Add(record);
            }

            return records;
        }

        private IRecordReader OpenOleDbReader()
        {
            string connectionString = AccessConnection.LooksLikeConnectionString(_definition.Location)
                ? _definition.Location
                : AccessConnection.ForDatabase(_definition.Location);

            var connection = new OleDbConnection(connectionString);
            connection.Open();
            var command = new OleDbCommand(_definition.Query, connection);
            OleDbDataReader reader = command.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            return new OleDbRecordReader(connection, command, reader);
        }

        private static string? Text(object? value)
        {
            if (value == null || value is DBNull)
            {
                return null;
            }

            string text = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString() ?? string.Empty;

            text = text.Trim();
            return text.Length == 0 ? null : text;
        }

        private sealed class OleDbRecordReader : IRecordReader, IDisposable
        {
            private readonly OleDbConnection _connection;
            private readonly OleDbCommand _command;
            private readonly OleDbDataReader _reader;
            private readonly Dictionary<string, int> _ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public OleDbRecordReader(OleDbConnection connection, OleDbCommand command, OleDbDataReader reader)
            {
                _connection = connection;
                _command = command;
                _reader = reader;

                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string name = reader.GetName(i);
                    columns.Add(name);
                    _ordinals[name] = i;
                }

                Columns = columns;
            }

            public IReadOnlyList<string> Columns { get; }

            public bool Read() => _reader.Read();

            public object? Value(string column) =>
                _ordinals.TryGetValue(column, out int ordinal) ? _reader.GetValue(ordinal) : null;

            public void Dispose()
            {
                _reader.Dispose();
                _command.Dispose();
                _connection.Dispose();
            }
        }
    }
}
