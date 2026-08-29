using System;
using System.Collections.Generic;
using System.Linq;

namespace ToleranceTool.Configuration.Aliases
{
    public enum AliasMatch
    {
        Exact = 0,
        Contains = 1,
        Regex = 2,
    }

    /// <summary>One System ID → signal rule. Exactly one of <see cref="SensorName"/> / <see cref="UniversalId"/> is set.</summary>
    public sealed class AliasEntry
    {
        public string SystemId { get; set; } = string.Empty;

        public string? SensorName { get; set; }

        public string? UniversalId { get; set; }

        public AliasMatch Match { get; set; } = AliasMatch.Exact;
    }

    /// <summary>A named, prioritized set of alias entries. Lower <see cref="Priority"/> numbers are consulted first.</summary>
    public sealed class AliasTable
    {
        public string Name { get; set; } = string.Empty;

        public int Priority { get; set; }

        public List<AliasEntry> Entries { get; } = new List<AliasEntry>();
    }

    /// <summary>All alias tables in play (workbook tables and shared tables), consulted in priority order.</summary>
    public sealed class AliasTableSet
    {
        private readonly List<AliasTable> _tables = new List<AliasTable>();

        public IReadOnlyList<AliasTable> Tables => _tables;

        public void Add(AliasTable table) => _tables.Add(table);

        /// <summary>Tables ordered by priority (ascending), then by insertion.</summary>
        public IEnumerable<AliasTable> InPriorityOrder() =>
            _tables
                .Select((t, i) => (Table: t, Index: i))
                .OrderBy(x => x.Table.Priority)
                .ThenBy(x => x.Index)
                .Select(x => x.Table);

        public static AliasTableSet Empty() => new AliasTableSet();
    }
}
