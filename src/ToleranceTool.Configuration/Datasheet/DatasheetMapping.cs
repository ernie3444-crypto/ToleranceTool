using System;
using System.Collections.Generic;
using ToleranceTool.Core.Precision;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Configuration.Datasheet
{
    /// <summary>The five required datasheet parameters. Column headers that carry them are configured per sheet.</summary>
    public enum DatasheetParameter
    {
        SystemId = 0,
        Expected = 1,
        Tolerance = 2,
        Actual = 3,
        PassFail = 4,
    }

    public enum DatasheetOrientation
    {
        /// <summary>One test case per row, headers in a header row. The launch format.</summary>
        RowPerCase = 0,

        /// <summary>One test case per column. P7.</summary>
        ColumnPerCase = 1,
    }

    /// <summary>
    /// Per-worksheet setup: which header carries which parameter, the data extent,
    /// the unit-system default, the precision policy, and any resolution overrides.
    /// Serialized into the workbook by <see cref="DatasheetMappingXml"/>.
    /// </summary>
    public sealed class DatasheetMapping
    {
        public DatasheetOrientation Orientation { get; set; } = DatasheetOrientation.RowPerCase;

        /// <summary>Zero-based index of the header row.</summary>
        public int HeaderRowIndex { get; set; }

        /// <summary>Zero-based first data row. Null → the row after the header.</summary>
        public int? FirstDataRowIndex { get; set; }

        /// <summary>Zero-based last data row (inclusive). Null → to the end of contiguous data.</summary>
        public int? LastDataRowIndex { get; set; }

        /// <summary>Parameter → the exact header text that carries it (matched case-insensitively, trimmed).</summary>
        public Dictionary<DatasheetParameter, string> Headers { get; } =
            new Dictionary<DatasheetParameter, string>();

        /// <summary>Optional header of a per-row unit column. Honoured when present and non-blank, never relied on.</summary>
        public string? UnitColumnHeader { get; set; }

        public UnitSystem DefaultUnitSystem { get; set; } = UnitSystem.English;

        public PrecisionPolicy Precision { get; set; } = PrecisionPolicy.MatchExpected();

        /// <summary>System ID → Universal ID corrections the user confirmed in the review grid. Sits above the whole ladder.</summary>
        public Dictionary<string, string> ResolutionOverrides { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string? Header(DatasheetParameter parameter) =>
            Headers.TryGetValue(parameter, out string value) ? value : null;

        /// <summary>The parameters that must be mapped before Apply/Check can run.</summary>
        public static readonly IReadOnlyList<DatasheetParameter> RequiredParameters = new[]
        {
            DatasheetParameter.SystemId,
            DatasheetParameter.Expected,
            DatasheetParameter.Tolerance,
        };

        public IReadOnlyList<DatasheetParameter> MissingRequiredHeaders()
        {
            var missing = new List<DatasheetParameter>();
            foreach (DatasheetParameter parameter in RequiredParameters)
            {
                if (string.IsNullOrWhiteSpace(Header(parameter)))
                {
                    missing.Add(parameter);
                }
            }

            return missing;
        }
    }
}
