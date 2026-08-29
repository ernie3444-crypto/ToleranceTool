using System.Collections.Generic;

namespace ToleranceTool.Core.Tolerances
{
    /// <summary>
    /// One entry in the tolerance library, keyed by signal type + module type.
    /// The band magnitude is the sum of the resolved <see cref="Terms"/>.
    /// </summary>
    public sealed class ToleranceDefinition
    {
        public string SignalType { get; set; } = string.Empty;

        public string ModuleType { get; set; } = string.Empty;

        public List<ToleranceTerm> Terms { get; } = new List<ToleranceTerm>();

        /// <summary>
        /// True when every term is EU-space, so the engine can skip the scale round-trip.
        /// </summary>
        public bool IsEuOnly
        {
            get
            {
                if (Terms.Count == 0)
                {
                    return false;
                }

                foreach (ToleranceTerm term in Terms)
                {
                    if (!term.IsEuSpace)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
