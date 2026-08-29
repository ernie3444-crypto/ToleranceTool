using System;
using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Datasheet;

namespace ToleranceTool.Excel.Datasheet
{
    /// <summary>
    /// Resolves mapped header text to column indexes by case-insensitive, trimmed match.
    /// The System ID header must resolve to exactly one column; the per-test-point
    /// headers (Expected / Tolerance / Actual / Pass-Fail) may repeat — each repeat is
    /// another test point on the same row.
    /// </summary>
    public sealed class ColumnResolver
    {
        /// <summary>Parameters whose header may appear more than once (one column group per test point).</summary>
        public static readonly IReadOnlyList<DatasheetParameter> Repeatable = new[]
        {
            DatasheetParameter.Expected,
            DatasheetParameter.Tolerance,
            DatasheetParameter.Actual,
            DatasheetParameter.PassFail,
        };

        private readonly Dictionary<DatasheetParameter, List<int>> _columns = new Dictionary<DatasheetParameter, List<int>>();
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

                bool repeatable = Repeatable.Contains(parameter);
                if (!byHeader.TryGetValue(header!.Trim(), out List<int> matches))
                {
                    _issues.Add(ConfigIssue.Error($"No column has the header \"{header}\" for {parameter}."));
                    continue;
                }

                if (!repeatable && matches.Count > 1)
                {
                    _issues.Add(ConfigIssue.Error(
                        $"More than one column has the header \"{header}\" for {parameter}, which must be unique."));
                    continue;
                }

                _columns[parameter] = matches.OrderBy(c => c).ToList();
            }

            if (!string.IsNullOrWhiteSpace(mapping.UnitColumnHeader)
                && byHeader.TryGetValue(mapping.UnitColumnHeader!.Trim(), out List<int> unitMatches))
            {
                if (unitMatches.Count > 1)
                {
                    _issues.Add(ConfigIssue.Error($"More than one column has the header \"{mapping.UnitColumnHeader}\" for the unit column."));
                }
                else
                {
                    UnitColumnIndex = unitMatches[0];
                }
            }
            else if (!string.IsNullOrWhiteSpace(mapping.UnitColumnHeader))
            {
                _issues.Add(ConfigIssue.Error($"No column has the header \"{mapping.UnitColumnHeader}\" for the unit column."));
            }

            TestPointCount = Math.Min(Columns(DatasheetParameter.Expected).Count, Columns(DatasheetParameter.Tolerance).Count);
        }

        public IReadOnlyList<ConfigIssue> Issues => _issues;

        public bool HasErrors => _issues.Any(i => i.Severity == ConfigSeverity.Error);

        public int? UnitColumnIndex { get; }

        /// <summary>Number of test-point column groups = min(#Expected, #Tolerance) columns.</summary>
        public int TestPointCount { get; }

        public IReadOnlyList<int> Columns(DatasheetParameter parameter) =>
            _columns.TryGetValue(parameter, out List<int> list) ? list : (IReadOnlyList<int>)Array.Empty<int>();

        public int? Column(DatasheetParameter parameter)
        {
            IReadOnlyList<int> list = Columns(parameter);
            return list.Count > 0 ? list[0] : (int?)null;
        }

        public int Require(DatasheetParameter parameter) =>
            Column(parameter) ?? throw new InvalidOperationException($"The {parameter} column did not resolve.");
    }
}
