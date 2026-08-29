using System;
using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Configuration.SignalTypes
{
    /// <summary>
    /// The signal-type registry: named signal types and the raw range each implies.
    /// The join uses it to fill <see cref="SignalConfig.RawLow"/> / <see cref="SignalConfig.RawHigh"/>
    /// when the import sources do not carry them.
    /// </summary>
    public sealed class SignalTypeRegistry
    {
        private readonly List<SignalTypeSpec> _specs = new List<SignalTypeSpec>();

        public IReadOnlyList<SignalTypeSpec> Specs => _specs;

        public int Count => _specs.Count;

        public void Add(SignalTypeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (TryGet(spec.Name, out _))
            {
                throw new InvalidOperationException($"Signal type \"{spec.Name}\" is already defined.");
            }

            _specs.Add(spec);
        }

        internal void AddRaw(SignalTypeSpec spec) => _specs.Add(spec);

        public bool Remove(SignalTypeSpec spec) => _specs.Remove(spec);

        public bool TryGet(string name, out SignalTypeSpec spec)
        {
            foreach (SignalTypeSpec candidate in _specs)
            {
                if (string.Equals(candidate.Name?.Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    spec = candidate;
                    return true;
                }
            }

            spec = null!;
            return false;
        }

        public IReadOnlyList<string> DuplicateNames() =>
            _specs
                .GroupBy(s => s.Name?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
    }
}
