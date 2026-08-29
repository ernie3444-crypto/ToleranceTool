using System;
using System.Collections.Generic;
using ToleranceTool.Core.Expressions;

namespace ToleranceTool.Core.Scales
{
    /// <summary>
    /// A compiled, ready-to-evaluate scaling curve. Built from a <see cref="ScaleType"/>;
    /// the engine calls <see cref="Forward"/> / <see cref="Inverse"/> per row.
    /// </summary>
    public sealed class ScaleCurve
    {
        private readonly ExpressionEvaluator _forward;
        private readonly ExpressionEvaluator _inverse;
        private readonly IReadOnlyDictionary<string, double> _parameters;

        public ScaleCurve(ScaleType scaleType)
        {
            if (scaleType == null)
            {
                throw new ArgumentNullException(nameof(scaleType));
            }

            Name = scaleType.Name;
            _parameters = new Dictionary<string, double>(scaleType.Parameters);
            _forward = new ExpressionEvaluator(scaleType.Forward);
            _inverse = new ExpressionEvaluator(scaleType.Inverse);
        }

        public string Name { get; }

        /// <summary>Maps an EU fraction to a raw fraction. Evaluated past [0, 1] when the band extrapolates.</summary>
        public double Forward(double euFraction) => Evaluate(_forward, euFraction);

        /// <summary>Maps a raw fraction back to an EU fraction. May return a non-finite value outside the domain.</summary>
        public double Inverse(double rawFraction) => Evaluate(_inverse, rawFraction);

        private double Evaluate(ExpressionEvaluator evaluator, double x)
        {
            var variables = new Dictionary<string, double>(_parameters.Count + 1) { ["x"] = x };
            foreach (KeyValuePair<string, double> pair in _parameters)
            {
                variables[pair.Key] = pair.Value;
            }

            return evaluator.Evaluate(variables);
        }

        /// <summary>
        /// Checks the library contract: <c>Forward(0) = 0</c>, <c>Forward(1) = 1</c>,
        /// and monotonic non-decreasing on <c>[0, 1]</c>. Returns the reasons it fails,
        /// empty when the curve is valid.
        /// </summary>
        public IReadOnlyList<string> Validate(int samples = 64, double tolerance = 1e-6)
        {
            var problems = new List<string>();

            double f0 = Safe(() => Forward(0));
            double f1 = Safe(() => Forward(1));

            if (!IsFinite(f0) || Math.Abs(f0) > tolerance)
            {
                problems.Add($"Forward(0) = {f0}, expected 0.");
            }

            if (!IsFinite(f1) || Math.Abs(f1 - 1) > tolerance)
            {
                problems.Add($"Forward(1) = {f1}, expected 1.");
            }

            double previous = f0;
            for (int i = 1; i <= samples; i++)
            {
                double x = (double)i / samples;
                double y = Safe(() => Forward(x));

                if (!IsFinite(y))
                {
                    problems.Add($"Forward({x:0.###}) is not a finite number.");
                    break;
                }

                if (y < previous - tolerance)
                {
                    problems.Add($"Forward is not monotonic: Forward({x:0.###}) = {y} < {previous}.");
                    break;
                }

                previous = y;
            }

            return problems;
        }

        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static double Safe(Func<double> evaluate)
        {
            try
            {
                return evaluate();
            }
            catch (ExpressionException)
            {
                return double.NaN;
            }
        }
    }
}
