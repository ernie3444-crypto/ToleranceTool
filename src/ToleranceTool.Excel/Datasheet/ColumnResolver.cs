using System;
using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Datasheet;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>Resolves mapped header text to column indexes by case-insensitive, trimmed match.</summary>
    public sealed class ColumnResolver
    {
        private readonly Dictionary<DatasheetParameter, int> _columns = new Dictionary<DatasheetParameter, int>();
        private readonly List<ConfigIssue> _issues = new List<ConfigIssue>();

        public ColumnResolver(DatasheetMapping mapping, string?[] headerRow)
        {
            var byHeader = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headerRow.Length; i++)
            {
                string text = headerRow[i]?.Trim() ?? string.Empty;
                if (text.Length == 0)
                {
                    continue;
                }

                if (!byHeader.TryGetValue(text, out List<int> list))
                {
                    list = new List<int>();
                    byHeader[text] = list;
                }

                list.Add(i);
            }

            foreach (DatasheetParameter parameter in Enum.GetValues(typeof(DatasheetParameter)).Cast<DatasheetParameter>())
            {
                string? header = mapping.Header(parameter);
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                Bind(parameter, parameter.ToString(), header!, byHeader);
            }

            if (!string.IsNullOrWhiteSpace(mapping.UnitColumnHeader))
            {
                if (Resolve(mapping.UnitColumnHeader!, byHeader, "unit column", out int unitColumn))
                {
                    UnitColumnIndex = unitColumn;
                }
            }
        }

        public IReadOnlyList<ConfigIssue> Issues => _issues;

        public bool HasErrors => _issues.Any(i => i.Severity == ConfigSeverity.Error);

        public int? UnitColumnIndex { get; }

        public int? Column(DatasheetParameter parameter) =>
            _columns.TryGetValue(parameter, out int index) ? index : (int?)null;

        public int Require(DatasheetParameter parameter) =>
            _columns.TryGetValue(parameter, out int index)
                ? index
                : throw new InvalidOperationException($"The {parameter} column did not resolve.");

        private void Bind(DatasheetParameter parameter, string label, string header, Dictionary<string, List<int>> byHeader)
        {
            if (Resolve(header, byHeader, label, out int column))
            {
                _columns[parameter] = column;
            }
        }

        private bool Resolve(string header, Dictionary<string, List<int>> byHeader, string label, out int column)
        {
            column = -1;
            if (!byHeader.TryGetValue(header.Trim(), out List<int> matches))
            {
                _issues.Add(ConfigIssue.Error($"No column has the header \"{header}\" for {label}."));
                return false;
            }

            if (matches.Count > 1)
            {
                _issues.Add(ConfigIssue.Error($"More than one column has the header \"{header}\" for {label}."));
                return false;
            }

            column = matches[0];
            return true;
        }
    }
}
