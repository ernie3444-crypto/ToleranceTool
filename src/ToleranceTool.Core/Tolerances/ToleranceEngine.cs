using System;
using System.Collections.Generic;
using ToleranceTool.Core.Expressions;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Core.Tolerances
{
    /// <summary>
    /// The P1 tolerance calculation. Two paths (see the architecture doc, §9):
    /// an EU fast path when every term is already in engineering units, and the
    /// raw round-trip when any term lives in raw units.
    /// </summary>
    public sealed class ToleranceEngine : IToleranceEngine
    {
        private readonly ScaleCurveLibrary _curves;

        public ToleranceEngine(ScaleCurveLibrary curves)
        {
            _curves = curves ?? throw new ArgumentNullException(nameof(curves));
        }

        public ToleranceResult Calculate(
            double expected,
            UnitSystem unitSystem,
            SignalConfig signal,
            ToleranceDefinition tolerance)
        {
            if (signal == null)
            {
                throw new ArgumentNullException(nameof(signal));
            }

            if (tolerance == null || tolerance.Terms.Count == 0)
            {
                return ToleranceResult.Failure(
                    ToleranceOutcome.NoToleranceMatch,
                    "No tolerance definition for this signal's type and module.");
            }

            if (!IsFinite(expected))
            {
                return ToleranceResult.Failure(
                    ToleranceOutcome.InvalidExpected,
                    "The expected value is missing or not a number.");
            }

            (double euLow, double euHigh) = signal.EuRange(unitSystem);
            double euSpan = euHigh - euLow;

            var result = new ToleranceResult
            {
                Expected = expected,
                UnitSystem = unitSystem,
            };

            var euTerms = new List<ToleranceTerm>();
            var rawTerms = new List<ToleranceTerm>();
            foreach (ToleranceTerm term in tolerance.Terms)
            {
                (term.IsEuSpace ? euTerms : rawTerms).Add(term);
            }

            try
            {
                return rawTerms.Count == 0
                    ? CalculateEuFastPath(result, expected, unitSystem, signal, euLow, euHigh, euTerms)
                    : CalculateRoundTrip(result, expected, unitSystem, signal, euLow, euHigh, euSpan, rawTerms, euTerms);
            }
            catch (ExpressionException ex)
            {
                return ToleranceResult.Failure(ToleranceOutcome.CurveUndefined, ex.Message);
            }
        }

        // --- Path A: every term in EU -------------------------------------------

        private static ToleranceResult CalculateEuFastPath(
            ToleranceResult result,
            double expected,
            UnitSystem unitSystem,
            SignalConfig signal,
            double euLow,
            double euHigh,
            List<ToleranceTerm> euTerms)
        {
            double band = 0;
            foreach (ToleranceTerm term in euTerms)
            {
                if (!TryResolveEuTerm(term, expected, unitSystem, signal, euLow, euHigh, out double magnitude, out string? error))
                {
                    return ToleranceResult.Failure(ToleranceOutcome.InvalidSignalConfig, error!);
                }

                band += magnitude;
            }

            result.UsedEuFastPath = true;
            result.Tolerance = band;
            result.EuPlus = expected + band;
            result.EuMinus = expected - band;
            return result;
        }

        // --- Path B: raw round-trip -------------------------------------------

        private ToleranceResult CalculateRoundTrip(
            ToleranceResult result,
            double expected,
            UnitSystem unitSystem,
            SignalConfig signal,
            double euLow,
            double euHigh,
            double euSpan,
            List<ToleranceTerm> rawTerms,
            List<ToleranceTerm> euTerms)
        {
            if (euSpan == 0)
            {
                return ToleranceResult.Failure(
                    ToleranceOutcome.InvalidSignalConfig,
                    "The signal's EU range has zero width, so the raw round-trip cannot run.");
            }

            if (signal.RawSpan == 0)
            {
                return ToleranceResult.Failure(
                    ToleranceOutcome.InvalidSignalConfig,
                    "The signal's raw range has zero width, so the raw round-trip cannot run.");
            }

            if (signal.ConversionSense == ConversionSense.Reverse
                && !string.Equals(signal.ScaleType, ScaleTypeNames.Linear, StringComparison.OrdinalIgnoreCase))
            {
                return ToleranceResult.Failure(
                    ToleranceOutcome.InvalidSignalConfig,
                    "Reverse conversion sense is only valid with the Linear scale type.");
            }

            if (!_curves.TryGet(signal.ScaleType, out ScaleCurve curve))
            {
                return ToleranceResult.Failure(
                    ToleranceOutcome.ScaleTypeUnknown,
                    $"Scale type \"{signal.ScaleType}\" is not in the scale-type library.");
            }

            double euFraction = (expected - euLow) / euSpan;
            double rawFraction = curve.Forward(euFraction);
            if (!IsFinite(rawFraction))
            {
                return ToleranceResult.Failure(
                    ToleranceOutcome.CurveUndefined,
                    "The scale curve is undefined at the expected value.");
            }

            double rawExpected = signal.ConversionSense == ConversionSense.Reverse
                ? signal.RawHigh - rawFraction * signal.RawSpan
                : signal.RawLow + rawFraction * signal.RawSpan;

            double rawBand = 0;
            foreach (ToleranceTerm term in rawTerms)
            {
                rawBand += ResolveRawTerm(term, expected, rawExpected, euLow, euHigh, signal);
            }

            double euExtra = 0;
            foreach (ToleranceTerm term in euTerms)
            {
                if (!TryResolveEuTerm(term, expected, unitSystem, signal, euLow, euHigh, out double magnitude, out string? error))
                {
                    return ToleranceResult.Failure(ToleranceOutcome.InvalidSignalConfig, error!);
                }

                euExtra += magnitude;
            }

            double rawPlus = rawExpected + rawBand;
            double rawMinus = rawExpected - rawBand;

            result.RawExpected = rawExpected;
            result.RawTolerance = rawBand;
            result.RawPlus = rawPlus;
            result.RawMinus = rawMinus;

            if (!TryRawEdgeToEu(rawPlus, curve, signal, euLow, euHigh, out double euEdgePlus)
                || !TryRawEdgeToEu(rawMinus, curve, signal, euLow, euHigh, out double euEdgeMinus))
            {
                result.Outcome = ToleranceOutcome.CurveUndefined;
                result.Message = "The tolerance band runs past the sensor range where the scale curve is undefined.";
                return result;
            }

            double euPlus = euEdgePlus + euExtra;
            double euMinus = euEdgeMinus - euExtra;

            double rawFloor = Math.Min(signal.RawLow, signal.RawHigh);
            double rawCeiling = Math.Max(signal.RawLow, signal.RawHigh);

            result.EuPlus = euPlus;
            result.EuMinus = euMinus;
            result.Extrapolated =
                rawPlus < rawFloor || rawPlus > rawCeiling ||
                rawMinus < rawFloor || rawMinus > rawCeiling;
            result.Tolerance = Math.Max(Math.Abs(euPlus - expected), Math.Abs(euMinus - expected));
            return result;
        }

        private static bool TryRawEdgeToEu(
            double rawEdge,
            ScaleCurve curve,
            SignalConfig signal,
            double euLow,
            double euHigh,
            out double euValue)
        {
            double rawFraction = signal.ConversionSense == ConversionSense.Reverse
                ? (signal.RawHigh - rawEdge) / signal.RawSpan
                : (rawEdge - signal.RawLow) / signal.RawSpan;

            double euFraction = curve.Inverse(rawFraction);
            if (!IsFinite(euFraction))
            {
                euValue = double.NaN;
                return false;
            }

            euValue = euLow + euFraction * (euHigh - euLow);
            return IsFinite(euValue);
        }

        // --- term resolution -------------------------------------------------

        private static bool TryResolveEuTerm(
            ToleranceTerm term,
            double expected,
            UnitSystem unitSystem,
            SignalConfig signal,
            double euLow,
            double euHigh,
            out double magnitude,
            out string? error)
        {
            error = null;
            double euSpan = euHigh - euLow;

            switch (term.Kind)
            {
                case ToleranceTermKind.AbsoluteEu:
                {
                    double value = Math.Abs(term.Value);
                    if (term.UnitSystem == unitSystem)
                    {
                        magnitude = value;
                        return true;
                    }

                    double rowSpan = unitSystem == UnitSystem.Si ? signal.EuSpanSi : signal.EuSpan;
                    double termSpan = term.UnitSystem == UnitSystem.Si ? signal.EuSpanSi : signal.EuSpan;
                    if (termSpan == 0)
                    {
                        magnitude = 0;
                        error = $"Cannot convert the \"{term.Unit}\" tolerance term: the signal has no {term.UnitSystem} EU span.";
                        return false;
                    }

                    magnitude = value * Math.Abs(rowSpan / termSpan);
                    return true;
                }

                case ToleranceTermKind.Percent:
                    magnitude = term.PercentBasis == PercentBasis.Reading
                        ? Math.Abs(term.Value * expected)
                        : Math.Abs(term.Value * euSpan);
                    return true;

                case ToleranceTermKind.Expression:
                    magnitude = Math.Abs(EvaluateExpressionTerm(term, expected, double.NaN, euLow, euHigh, signal));
                    return true;

                default:
                    magnitude = 0;
                    error = $"Term kind {term.Kind} is not an EU-space term.";
                    return false;
            }
        }

        private static double ResolveRawTerm(
            ToleranceTerm term,
            double expected,
            double rawExpected,
            double euLow,
            double euHigh,
            SignalConfig signal)
        {
            switch (term.Kind)
            {
                case ToleranceTermKind.AbsoluteRaw:
                    return Math.Abs(term.Value);

                case ToleranceTermKind.Percent:
                    return term.PercentBasis == PercentBasis.Reading
                        ? Math.Abs(term.Value * rawExpected)
                        : Math.Abs(term.Value * signal.RawSpan);

                case ToleranceTermKind.Expression:
                    return Math.Abs(EvaluateExpressionTerm(term, expected, rawExpected, euLow, euHigh, signal));

                default:
                    return 0;
            }
        }

        private static double EvaluateExpressionTerm(
            ToleranceTerm term,
            double expected,
            double rawExpected,
            double euLow,
            double euHigh,
            SignalConfig signal)
        {
            var evaluator = new ExpressionEvaluator(term.ExpressionBody);
            var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["expected"] = expected,
                ["rawExpected"] = rawExpected,
                ["rawLow"] = signal.RawLow,
                ["rawHigh"] = signal.RawHigh,
                ["rawSpan"] = signal.RawSpan,
                ["euLow"] = euLow,
                ["euHigh"] = euHigh,
                ["euSpan"] = euHigh - euLow,
            };

            return evaluator.Evaluate(variables);
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
