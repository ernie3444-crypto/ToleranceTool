using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Import
{
    /// <summary>
    /// Joins signal sources on Universal ID (left join from the master) and maps the
    /// merged field values onto <see cref="SignalConfig"/>, recording every gap.
    /// </summary>
    public sealed class SignalSetBuilder
    {
        private readonly List<ISignalSource> _sources = new List<ISignalSource>();
        private readonly Dictionary<string, ImportSourceDefinition> _definitions =
            new Dictionary<string, ImportSourceDefinition>(StringComparer.Ordinal);

        public SignalSetBuilder Add(ISignalSource source, ImportSourceDefinition? definition = null)
        {
            _sources.Add(source);
            if (definition != null)
            {
                _definitions[source.Name] = definition;
            }

            return this;
        }

        public ConfigLoadResult<ResolvedSignalSet> Build()
        {
            var issues = new List<ConfigIssue>();

            List<ISignalSource> masters = _sources.Where(s => s.IsMaster).ToList();
            if (masters.Count == 0)
            {
                issues.Add(ConfigIssue.Error("No master source. One source must link Sensor Name to Universal ID."));
                return new ConfigLoadResult<ResolvedSignalSet>(new ResolvedSignalSet(Array.Empty<ResolvedSignal>()), issues);
            }

            if (masters.Count > 1)
            {
                issues.Add(ConfigIssue.Error(
                    $"More than one master source: {string.Join(", ", masters.Select(m => m.Name))}."));
                return new ConfigLoadResult<ResolvedSignalSet>(new ResolvedSignalSet(Array.Empty<ResolvedSignal>()), issues);
            }

            ISignalSource master = masters[0];

            // Read every source once; index the non-master ones by Universal ID.
            var perSource = new List<(ISignalSource Source, IReadOnlyList<SignalFieldRecord> Records)>();
            foreach (ISignalSource source in _sources)
            {
                try
                {
                    perSource.Add((source, source.Read()));
                }
                catch (Exception ex)
                {
                    issues.Add(ConfigIssue.Error($"Could not read source \"{source.Name}\": {ex.Message}"));
                }
            }

            if (issues.Any(i => i.Severity == ConfigSeverity.Error))
            {
                return new ConfigLoadResult<ResolvedSignalSet>(new ResolvedSignalSet(Array.Empty<ResolvedSignal>()), issues);
            }

            var lookups = new List<(ISignalSource Source, Dictionary<string, SignalFieldRecord> ById)>();
            foreach ((ISignalSource source, IReadOnlyList<SignalFieldRecord> records) in perSource)
            {
                if (ReferenceEquals(source, master))
                {
                    continue;
                }

                var byId = new Dictionary<string, SignalFieldRecord>(StringComparer.OrdinalIgnoreCase);
                foreach (SignalFieldRecord record in records)
                {
                    if (byId.ContainsKey(record.UniversalId))
                    {
                        issues.Add(ConfigIssue.Warning(
                            $"Source \"{source.Name}\" has more than one row for Universal ID \"{record.UniversalId}\"; the first is used."));
                        continue;
                    }

                    byId[record.UniversalId] = record;
                }

                lookups.Add((source, byId));
            }

            IReadOnlyList<SignalFieldRecord> masterRecords =
                perSource.First(p => ReferenceEquals(p.Source, master)).Records;

            var resolved = new List<ResolvedSignal>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (SignalFieldRecord masterRecord in masterRecords)
            {
                if (!seen.Add(masterRecord.UniversalId))
                {
                    issues.Add(ConfigIssue.Warning(
                        $"The master lists Universal ID \"{masterRecord.UniversalId}\" more than once; the first is used."));
                    continue;
                }

                var merged = new Dictionary<string, (string? Value, string Source)>(StringComparer.Ordinal);
                Merge(merged, masterRecord, master.Name);

                foreach ((ISignalSource source, Dictionary<string, SignalFieldRecord> byId) in lookups)
                {
                    if (byId.TryGetValue(masterRecord.UniversalId, out SignalFieldRecord? record))
                    {
                        Merge(merged, record, source.Name);
                    }
                }

                resolved.Add(MapSignal(masterRecord.UniversalId, merged));
            }

            return new ConfigLoadResult<ResolvedSignalSet>(new ResolvedSignalSet(resolved), issues);
        }

        private static void Merge(
            Dictionary<string, (string? Value, string Source)> merged, SignalFieldRecord record, string sourceName)
        {
            foreach (KeyValuePair<string, string?> field in record.Fields)
            {
                // First non-null wins; master is merged first.
                if (!merged.TryGetValue(field.Key, out (string? Value, string Source) existing) || existing.Value == null)
                {
                    merged[field.Key] = (field.Value, sourceName);
                }
            }
        }

        private ResolvedSignal MapSignal(string universalId, Dictionary<string, (string? Value, string Source)> merged)
        {
            var config = new SignalConfig { UniversalId = universalId };
            var gaps = new List<FieldGap>();

            bool Has(string field, out string value, out string source)
            {
                if (merged.TryGetValue(field, out (string? Value, string Source) hit) && hit.Value != null)
                {
                    value = hit.Value;
                    source = hit.Source;
                    return true;
                }

                value = string.Empty;
                source = string.Empty;
                return false;
            }

            void RequireText(string field, Action<string> set, bool required)
            {
                if (Has(field, out string value, out _))
                {
                    set(value);
                }
                else if (required)
                {
                    gaps.Add(new FieldGap(universalId, field, "no value in any source"));
                }
            }

            void RequireNumber(string field, Action<double> set, bool required)
            {
                if (Has(field, out string value, out string source))
                {
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    {
                        set(number);
                    }
                    else
                    {
                        gaps.Add(new FieldGap(universalId, field, $"\"{value}\" from {source} is not a number"));
                    }
                }
                else if (required)
                {
                    gaps.Add(new FieldGap(universalId, field, "no value in any source"));
                }
            }

            bool RequiredFor(string field)
            {
                var bindings = _definitions.Values
                    .Select(d => d.Binding(field))
                    .Where(b => b != null)
                    .ToList();

                if (bindings.Count > 0)
                {
                    return bindings.Any(b => b!.Required);
                }

                // Not bound anywhere (or no definitions supplied): fall back to the field's default.
                return SignalField.Find(field)?.RequiredByDefault ?? false;
            }

            RequireText(SignalField.SensorName, v => config.SensorName = v, RequiredFor(SignalField.SensorName));
            RequireText(SignalField.ScaleType, v => config.ScaleType = v, RequiredFor(SignalField.ScaleType));
            RequireText(SignalField.SignalType, v => config.SignalType = v, RequiredFor(SignalField.SignalType));
            RequireText(SignalField.ModuleType, v => config.ModuleType = v, RequiredFor(SignalField.ModuleType));

            if (Has(SignalField.ConversionSense, out string senseText, out string senseSource))
            {
                if (TryParseSense(senseText, out ConversionSense sense))
                {
                    config.ConversionSense = sense;
                }
                else
                {
                    gaps.Add(new FieldGap(universalId, SignalField.ConversionSense,
                        $"\"{senseText}\" from {senseSource} is not Direct or Reverse"));
                }
            }
            else if (RequiredFor(SignalField.ConversionSense))
            {
                gaps.Add(new FieldGap(universalId, SignalField.ConversionSense, "no value in any source"));
            }

            RequireNumber(SignalField.RawLow, v => config.RawLow = v, RequiredFor(SignalField.RawLow));
            RequireNumber(SignalField.RawHigh, v => config.RawHigh = v, RequiredFor(SignalField.RawHigh));
            RequireNumber(SignalField.EuLow, v => config.EuLow = v, RequiredFor(SignalField.EuLow));
            RequireNumber(SignalField.EuHigh, v => config.EuHigh = v, RequiredFor(SignalField.EuHigh));
            RequireNumber(SignalField.EuLowSi, v => config.EuLowSi = v, RequiredFor(SignalField.EuLowSi));
            RequireNumber(SignalField.EuHighSi, v => config.EuHighSi = v, RequiredFor(SignalField.EuHighSi));

            return new ResolvedSignal(config, gaps);
        }

        private static bool TryParseSense(string text, out ConversionSense sense)
        {
            string t = text.Trim();
            if (string.Equals(t, "Direct", StringComparison.OrdinalIgnoreCase))
            {
                sense = ConversionSense.Direct;
                return true;
            }

            if (string.Equals(t, "Reverse", StringComparison.OrdinalIgnoreCase))
            {
                sense = ConversionSense.Reverse;
                return true;
            }

            sense = ConversionSense.Direct;
            return false;
        }
    }
}
