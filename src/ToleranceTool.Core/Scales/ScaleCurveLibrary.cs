using System;
using System.Collections.Generic;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Core.Scales
{
    /// <summary>
    /// The set of scaling curves available to the engine, keyed by name
    /// (case-insensitive). Populated from the scale-type library XML in P5; the
    /// three well-known curves are provided by <see cref="CreateDefault"/> so the
    /// engine and its tests have something to run against now.
    /// </summary>
    public sealed class ScaleCurveLibrary
    {
        private readonly Dictionary<string, ScaleCurve> _curves =
            new Dictionary<string, ScaleCurve>(StringComparer.OrdinalIgnoreCase);

        public void Add(ScaleType scaleType)
        {
            if (scaleType == null)
            {
                throw new ArgumentNullException(nameof(scaleType));
            }

            if (string.IsNullOrWhiteSpace(scaleType.Name))
            {
                throw new ArgumentException("A scale type must have a name.", nameof(scaleType));
            }

            _curves[scaleType.Name] = new ScaleCurve(scaleType);
        }

        public bool TryGet(string name, out ScaleCurve curve)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return _curves.TryGetValue(name, out curve);
            }

            curve = null!;
            return false;
        }

        public bool Contains(string name) =>
            !string.IsNullOrWhiteSpace(name) && _curves.ContainsKey(name);

        /// <summary>The built-in curves: Linear, SquareRoot, and a 2-decade Logarithmic.</summary>
        public static ScaleCurveLibrary CreateDefault()
        {
            var library = new ScaleCurveLibrary();

            library.Add(new ScaleType
            {
                Name = ScaleTypeNames.Linear,
                Forward = "x",
                Inverse = "x",
            });

            library.Add(new ScaleType
            {
                Name = ScaleTypeNames.SquareRoot,
                Forward = "Pow(x, 2)",
                Inverse = "Sqrt(x)",
            });

            var logarithmic = new ScaleType
            {
                Name = ScaleTypeNames.Logarithmic,
                Forward = "(Pow(10, x * decades) - 1) / (Pow(10, decades) - 1)",
                Inverse = "Log10(x * (Pow(10, decades) - 1) + 1) / decades",
            };
            logarithmic.Parameters["decades"] = 2;
            library.Add(logarithmic);

            return library;
        }
    }
}
