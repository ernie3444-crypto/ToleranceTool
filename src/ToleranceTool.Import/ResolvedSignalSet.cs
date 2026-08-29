using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Import
{
    /// <summary>Why one field of one signal did not resolve.</summary>
    public sealed class FieldGap
    {
        public FieldGap(string universalId, string field, string reason)
        {
            UniversalId = universalId;
            Field = field;
            Reason = reason;
        }

        public string UniversalId { get; }

        public string Field { get; }

        public string Reason { get; }

        public override string ToString() => $"{UniversalId} / {Field}: {Reason}";
    }

    /// <summary>One signal after the join: its config, and whether every required field landed.</summary>
    public sealed class ResolvedSignal
    {
        public ResolvedSignal(SignalConfig config, IReadOnlyList<FieldGap> gaps)
        {
            Config = config;
            Gaps = gaps;
        }

        public SignalConfig Config { get; }

        public IReadOnlyList<FieldGap> Gaps { get; }

        public bool IsComplete => Gaps.Count == 0;
    }

    /// <summary>The joined, validated collection the calculation consumes.</summary>
    public sealed class ResolvedSignalSet
    {
        public ResolvedSignalSet(IReadOnlyList<ResolvedSignal> signals)
        {
            Signals = signals;
        }

        public IReadOnlyList<ResolvedSignal> Signals { get; }

        public int Count => Signals.Count;

        public IEnumerable<ResolvedSignal> Complete => Signals.Where(s => s.IsComplete);

        public IEnumerable<ResolvedSignal> Incomplete => Signals.Where(s => !s.IsComplete);

        public IEnumerable<FieldGap> AllGaps => Signals.SelectMany(s => s.Gaps);

        public bool IsReady => Signals.Count > 0 && Signals.All(s => s.IsComplete);

        /// <summary>Distinct (SignalType, ModuleType) pairs across the complete signals — the tolerance-library demand.</summary>
        public IEnumerable<(string SignalType, string ModuleType)> RequiredTolerances() =>
            Complete
                .Select(s => (s.Config.SignalType, s.Config.ModuleType))
                .Distinct();

        public ResolvedSignal? Find(string universalId) =>
            Signals.FirstOrDefault(s =>
                string.Equals(s.Config.UniversalId, universalId, System.StringComparison.OrdinalIgnoreCase));
    }
}
