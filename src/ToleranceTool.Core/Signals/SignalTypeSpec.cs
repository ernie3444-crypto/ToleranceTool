namespace ToleranceTool.Core.Signals
{
    /// <summary>
    /// One entry in the signal-type registry: a named signal type and the raw
    /// range it implies.
    /// </summary>
    public sealed class SignalTypeSpec
    {
        public string Name { get; set; } = string.Empty;

        public double RawLow { get; set; }

        public double RawHigh { get; set; }

        /// <summary>Raw unit label, e.g. "mA", "V".</summary>
        public string Unit { get; set; } = string.Empty;
    }
}
