namespace ToleranceTool.Core.Signals
{
    /// <summary>
    /// Which engineering-unit range a datasheet row is expressed in.
    /// </summary>
    public enum UnitSystem
    {
        /// <summary>Display / imperial units — uses EuLow / EuHigh.</summary>
        English = 0,

        /// <summary>SI units — uses EuLowSi / EuHighSi.</summary>
        Si = 1,
    }
}
