namespace ToleranceTool.Core.Signals
{
    /// <summary>
    /// Well-known scale-type names. Scale types are configuration-driven, so this
    /// is a convenience list, not a closed set — any name present in the scale-type
    /// library is valid.
    /// </summary>
    public static class ScaleTypeNames
    {
        public const string Linear = "Linear";
        public const string SquareRoot = "SquareRoot";
        public const string Logarithmic = "Logarithmic";
    }
}
