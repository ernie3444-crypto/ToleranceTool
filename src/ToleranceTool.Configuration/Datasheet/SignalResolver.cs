using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ToleranceTool.Configuration.Aliases;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Configuration.Datasheet
{
    public enum ResolutionStep
    {
        /// <summary>A per-sheet override map entry matched.</summary>
        Override = 0,

        /// <summary>The System ID equalled a Sensor Name exactly (trimmed, case-insensitive).</summary>
        Exact = 1,

        /// <summary>An alias table entry matched.</summary>
        Alias = 2,

        /// <summary>Exactly one Sensor Name occurred as a whole-token substring of the System ID.</summary>
        AutoMatch = 3,

        /// <summary>Nothing matched.</summary>
        Unresolved = 4,

        /// <summary>Two or more candidates — never guessed.</summary>
        Ambiguous = 5,

        /// <summary>The user marked this System ID to be skipped (override value = <see cref="SignalResolver.ExcludeMarker"/>).</summary>
        Excluded = 6,
    }

    public sealed class SignalResolution
    {
        public SignalResolution(string systemId, ResolutionStep step, SignalConfig? signal, IReadOnlyList<string> candidates)
        {
            SystemId = systemId;
            Step = step;
            Signal = signal;
            Candidates = candidates;
        }

        public string SystemId { get; }

        public ResolutionStep Step { get; }

        public SignalConfig? Signal { get; }

        /// <summary>The competing Sensor Names when <see cref="Step"/> is <see cref="ResolutionStep.Ambiguous"/>.</summary>
        public IReadOnlyList<string> Candidates { get; }

        public bool IsResolved => Signal != null;
    }

    /// <summary>
    /// The System ID → signal resolution ladder (architecture doc §5):
    /// per-sheet override → exact Sensor Name → alias tables by priority → auto-match.
    /// </summary>
    public sealed class SignalResolver
    {
        /// <summary>Override value that marks a System ID to be left alone by Apply / Check.</summary>
        public const string ExcludeMarker = "(skip)";

        private readonly List<SignalConfig> _signals;
        private readonly AliasTableSet _aliases;
        private readonly IReadOnlyDictionary<string, string> _overrides;
        private readonly Dictionary<string, SignalConfig> _bySensorName;
        private readonly Dictionary<string, SignalConfig> _byUniversalId;

        public SignalResolver(
            IEnumerable<SignalConfig> signals,
            AliasTableSet? aliases = null,
            IReadOnlyDictionary<string, string>? overrides = null)
        {
            _signals = signals.ToList();
            _aliases = aliases ?? AliasTableSet.Empty();
            _overrides = overrides ?? new Dictionary<string, string>();

            _bySensorName = new Dictionary<string, SignalConfig>(StringComparer.OrdinalIgnoreCase);
            _byUniversalId = new Dictionary<string, SignalConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (SignalConfig signal in _signals)
            {
                if (!string.IsNullOrWhiteSpace(signal.SensorName))
                {
                    _bySensorName[signal.SensorName.Trim()] = signal;
                }

                if (!string.IsNullOrWhiteSpace(signal.UniversalId))
                {
                    _byUniversalId[signal.UniversalId.Trim()] = signal;
                }
            }
        }

        public SignalResolution Resolve(string systemId)
        {
            string key = (systemId ?? string.Empty).Trim();

            if (_overrides.TryGetValue(key, out string overrideValue))
            {
                if (string.Equals(overrideValue?.Trim(), ExcludeMarker, StringComparison.OrdinalIgnoreCase))
                {
                    return new SignalResolution(key, ResolutionStep.Excluded, null, Array.Empty<string>());
                }

                if (_byUniversalId.TryGetValue((overrideValue ?? string.Empty).Trim(), out SignalConfig overridden))
                {
                    return new SignalResolution(key, ResolutionStep.Override, overridden, Array.Empty<string>());
                }
            }

            if (_bySensorName.TryGetValue(key, out SignalConfig exact))
            {
                return new SignalResolution(key, ResolutionStep.Exact, exact, Array.Empty<string>());
            }

            foreach (AliasTable table in _aliases.InPriorityOrder())
            {
                foreach (AliasEntry entry in table.Entries)
                {
                    if (!AliasMatches(entry, key, out string? capturedSensorName))
                    {
                        continue;
                    }

                    SignalConfig? signal = ResolveAliasTarget(entry, capturedSensorName);
                    if (signal != null)
                    {
                        return new SignalResolution(key, ResolutionStep.Alias, signal, Array.Empty<string>());
                    }
                }
            }

            List<SignalConfig> tokenMatches = _signals
                .Where(s => !string.IsNullOrWhiteSpace(s.SensorName) && ContainsWholeToken(key, s.SensorName.Trim()))
                .ToList();

            if (tokenMatches.Count == 1)
            {
                return new SignalResolution(key, ResolutionStep.AutoMatch, tokenMatches[0], Array.Empty<string>());
            }

            if (tokenMatches.Count > 1)
            {
                return new SignalResolution(
                    key,
                    ResolutionStep.Ambiguous,
                    null,
                    tokenMatches.Select(s => s.SensorName).ToList());
            }

            return new SignalResolution(key, ResolutionStep.Unresolved, null, Array.Empty<string>());
        }

        private SignalConfig? ResolveAliasTarget(AliasEntry entry, string? capturedSensorName)
        {
            if (entry.UniversalId != null)
            {
                return _byUniversalId.TryGetValue(entry.UniversalId.Trim(), out SignalConfig byId) ? byId : null;
            }

            string sensorName = (capturedSensorName ?? entry.SensorName ?? string.Empty).Trim();
            return _bySensorName.TryGetValue(sensorName, out SignalConfig bySensor) ? bySensor : null;
        }

        private static bool AliasMatches(AliasEntry entry, string systemId, out string? capturedSensorName)
        {
            capturedSensorName = null;

            switch (entry.Match)
            {
                case AliasMatch.Exact:
                    return string.Equals(entry.SystemId.Trim(), systemId, StringComparison.OrdinalIgnoreCase);

                case AliasMatch.Contains:
                    return systemId.IndexOf(entry.SystemId.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

                case AliasMatch.Regex:
                    try
                    {
                        Match match = Regex.Match(systemId, entry.SystemId, RegexOptions.IgnoreCase);
                        if (!match.Success)
                        {
                            return false;
                        }

                        if (entry.SensorName != null && entry.SensorName.Contains("$"))
                        {
                            capturedSensorName = match.Result(entry.SensorName);
                        }

                        return true;
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }

                default:
                    return false;
            }
        }

        /// <summary>True when <paramref name="sensorName"/> appears in <paramref name="systemId"/> bounded by non-alphanumerics.</summary>
        internal static bool ContainsWholeToken(string systemId, string sensorName)
        {
            if (sensorName.Length == 0)
            {
                return false;
            }

            int index = systemId.IndexOf(sensorName, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                char before = index > 0 ? systemId[index - 1] : ' ';
                int afterPos = index + sensorName.Length;
                char after = afterPos < systemId.Length ? systemId[afterPos] : ' ';

                if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after))
                {
                    return true;
                }

                index = systemId.IndexOf(sensorName, index + 1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
