using System;
using System.Collections.Generic;

namespace ToleranceTool.Import
{
    public enum SignalSourceKind
    {
        DelimitedText = 0,
        Workbook = 1,
        Access = 2,
    }

    public enum SignalDataOrientation
    {
        /// <summary>One signal per row, fields in columns. The launch format.</summary>
        RowPerSignal = 0,

        /// <summary>One signal per column, field labels down a column (key/value sheets).</summary>
        ColumnPerSignal = 1,

        /// <summary>
        /// One row per parameter: a key column, a parameter-name column, a value
        /// column, and an optional metric (SI) value column. Several rows build one
        /// signal, grouped by the key.
        /// </summary>
        ParameterPerRow = 2,
    }

    /// <summary>How one source contributes to the join: where its key is, and which field sits in which column.</summary>
    public sealed class FieldBinding
    {
        public FieldBinding(string field, string locator, bool required)
        {
            Field = field;
            Locator = locator;
            Required = required;
        }

        /// <summary>A <see cref="SignalField"/> name.</summary>
        public string Field { get; }

        /// <summary>Column letter / 1-based number for a row-oriented source; row number for a column-oriented one.</summary>
        public string Locator { get; set; }

        public bool Required { get; set; }
    }

    /// <summary>
    /// A user-configured signal source: a file (or Access DB), its orientation, the
    /// key location, and the field-to-column bindings. Stored in the workbook.
    /// </summary>
    public sealed class ImportSourceDefinition
    {
        public ImportSourceDefinition(string name, SignalSourceKind kind, string location)
        {
            Name = name;
            Kind = kind;
            Location = location;
        }

        public string Name { get; set; }

        public SignalSourceKind Kind { get; set; }

        /// <summary>File path, or an Access connection string.</summary>
        public string Location { get; set; }

        /// <summary>For a workbook: which sheet. Null = first sheet.</summary>
        public string? SheetName { get; set; }

        public SignalDataOrientation Orientation { get; set; } = SignalDataOrientation.RowPerSignal;

        /// <summary>Zero-based index of the header row (row-oriented) — data starts on the next row. Null = no header.</summary>
        public int? HeaderRowIndex { get; set; } = 0;

        /// <summary>
        /// Where this source's Universal ID lives: a column letter / number for a
        /// file, or the result-set column name for an Access query.
        /// </summary>
        public string UniversalIdLocator { get; set; } = "A";

        /// <summary>The SQL that produces the result set for an <see cref="SignalSourceKind.Access"/> source.</summary>
        public string? Query { get; set; }

        // --- ParameterPerRow layout only ---

        /// <summary>Column holding the parameter name on each row.</summary>
        public string? ParameterNameLocator { get; set; }

        /// <summary>Column holding the parameter value (display / English units) on each row.</summary>
        public string? ParameterValueLocator { get; set; }

        /// <summary>Optional column holding the metric (SI) value on each row.</summary>
        public string? ParameterMetricLocator { get; set; }

        /// <summary>Exactly one source in a set is the master (carries Sensor Name, sets the signal count).</summary>
        public bool IsMaster { get; set; }

        public List<FieldBinding> Fields { get; } = new List<FieldBinding>();

        public FieldBinding? Binding(string field) =>
            Fields.Find(b => string.Equals(b.Field, field, StringComparison.Ordinal));
    }
}
