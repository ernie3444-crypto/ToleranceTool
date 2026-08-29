using System.Collections.Generic;

namespace ToleranceTool.Core.Scales
{
    /// <summary>
    /// One entry in the scale-type library: the two directions of a signal's
    /// scaling curve, as expressions over a single normalized variable
    /// <c>x</c> in <c>[0, 1]</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Forward"/> maps the EU fraction to the raw fraction;
    /// <see cref="Inverse"/> is its reciprocal. Extra constants the expressions
    /// reference (e.g. <c>decades</c>) are supplied through <see cref="Parameters"/>.
    /// </remarks>
    public sealed class ScaleType
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>euFrac &rarr; rawFrac.</summary>
        public string Forward { get; set; } = "x";

        /// <summary>rawFrac &rarr; euFrac.</summary>
        public string Inverse { get; set; } = "x";

        /// <summary>Named constants available to both expressions in addition to <c>x</c>.</summary>
        public Dictionary<string, double> Parameters { get; } = new Dictionary<string, double>();
    }
}
