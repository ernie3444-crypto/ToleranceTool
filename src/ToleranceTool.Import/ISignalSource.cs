using System.Collections.Generic;

namespace ToleranceTool.Import
{
    /// <summary>
    /// One source of signal-configuration field values, keyed by Universal ID.
    /// Implemented by the file-based and Access sources in P4 / P6.
    /// </summary>
    public interface ISignalSource
    {
        string Name { get; }

        /// <summary>
        /// True for the single source that links Sensor Name to Universal ID and
        /// therefore decides how many signals exist.
        /// </summary>
        bool IsMaster { get; }

        IReadOnlyList<SignalFieldRecord> Read();
    }

    /// <summary>The field values one source contributes for one Universal ID.</summary>
    public sealed class SignalFieldRecord
    {
        public string UniversalId { get; set; } = string.Empty;

        /// <summary>Field name (e.g. "SensorName") to raw string value, as read from the source.</summary>
        public Dictionary<string, string?> Fields { get; } = new Dictionary<string, string?>();
    }
}
