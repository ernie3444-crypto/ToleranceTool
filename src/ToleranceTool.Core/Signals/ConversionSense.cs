namespace ToleranceTool.Core.Signals
{
    /// <summary>
    /// Direction of the EU-to-raw relationship for a signal.
    /// </summary>
    public enum ConversionSense
    {
        /// <summary>Low EU maps to low raw, high EU maps to high raw.</summary>
        Direct = 0,

        /// <summary>
        /// Low EU maps to high raw, high EU maps to low raw.
        /// Only valid with <see cref="ScaleTypeNames.Linear"/>.
        /// </summary>
        Reverse = 1,
    }
}
