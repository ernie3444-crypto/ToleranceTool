using System;
using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.Configuration.Tolerances
{
    /// <summary>
    /// The in-memory tolerance library: the set of <see cref="ToleranceDefinition"/>
    /// keyed by <c>signalType + moduleType</c> (case-insensitive). Editor operations
    /// (add / delete / modify) work directly on <see cref="Definitions"/>.
    /// </summary>
    public sealed class ToleranceLibrary
    {
        private readonly List<ToleranceDefinition> _definitions = new List<ToleranceDefinition>();

        public IReadOnlyList<ToleranceDefinition> Definitions => _definitions;

        public int Count => _definitions.Count;

        public static string KeyOf(string signalType, string moduleType) =>
            $"{signalType?.Trim()} / {moduleType?.Trim()}";

        public static string KeyOf(ToleranceDefinition definition) =>
            KeyOf(definition.SignalType, definition.ModuleType);

        /// <summary>Adds a definition. Throws when its key collides with one already present.</summary>
        public void Add(ToleranceDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (TryGet(definition.SignalType, definition.ModuleType, out _))
            {
                throw new InvalidOperationException(
                    $"A tolerance for \"{KeyOf(definition)}\" is already defined.");
            }

            _definitions.Add(definition);
        }

        /// <summary>Adds a definition without the duplicate check — used by the loader, which reports duplicates itself.</summary>
        internal void AddRaw(ToleranceDefinition definition) => _definitions.Add(definition);

        public bool Remove(ToleranceDefinition definition) => _definitions.Remove(definition);

        public bool TryGet(string signalType, string moduleType, out ToleranceDefinition definition)
        {
            foreach (ToleranceDefinition candidate in _definitions)
            {
                if (Equals(candidate.SignalType, signalType) && Equals(candidate.ModuleType, moduleType))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null!;
            return false;
        }

        /// <summary>
        /// The <c>(signalType, moduleType)</c> pairs from <paramref name="required"/> that
        /// have no definition. This is the gate for "Tolerance Configuration is ready".
        /// </summary>
        public IReadOnlyList<(string SignalType, string ModuleType)> MissingFor(
            IEnumerable<(string SignalType, string ModuleType)> required)
        {
            var missing = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((string signalType, string moduleType) in required)
            {
                string key = KeyOf(signalType, moduleType);
                if (seen.Add(key) && !TryGet(signalType, moduleType, out _))
                {
                    missing.Add((signalType, moduleType));
                }
            }

            return missing;
        }

        /// <summary>Keys that appear more than once across <see cref="Definitions"/>.</summary>
        public IReadOnlyList<string> DuplicateKeys() =>
            _definitions
                .GroupBy(KeyOf, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

        private static bool Equals(string a, string b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
