using System;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;
using Xunit;

namespace ToleranceTool.Tests
{
    public class ToleranceEngineTests
    {
        private readonly ToleranceEngine _engine = new ToleranceEngine(ScaleCurveLibrary.CreateDefault());

        private static SignalConfig Signal(
            string scaleType = ScaleTypeNames.Linear,
            ConversionSense sense = ConversionSense.Direct,
            double rawLow = 4,
            double rawHigh = 20,
            double euLow = 0,
            double euHigh = 250,
            double euLowSi = 0,
            double euHighSi = 250) => new SignalConfig
            {
                ScaleType = scaleType,
                ConversionSense = sense,
                SignalType = "4-20mA",
                ModuleType = "AI-871",
                RawLow = rawLow,
                RawHigh = rawHigh,
                EuLow = euLow,
                EuHigh = euHigh,
                EuLowSi = euLowSi,
                EuHighSi = euHighSi,
            };

        private static ToleranceDefinition Percent(double fraction, PercentBasis basis = PercentBasis.RawSpan)
        {
            var def = new ToleranceDefinition { SignalType = "4-20mA", ModuleType = "AI-871" };
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.Percent, Value = fraction, PercentBasis = basis });
            return def;
        }

        // --- Path A: EU fast path ------------------------------------------------

        [Fact]
        public void EuOnly_AbsoluteEu_IsAppliedSymmetricallyWithNoRoundTrip()
        {
            var def = new ToleranceDefinition();
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.AbsoluteEu, Value = 0.45, Unit = "degF", UnitSystem = UnitSystem.English });

            ToleranceResult result = _engine.Calculate(137.2, UnitSystem.English, Signal(), def);

            Assert.Equal(ToleranceOutcome.Calculated, result.Outcome);
            Assert.True(result.UsedEuFastPath);
            Assert.Equal(0.45, result.Tolerance, 10);
            Assert.Equal(137.65, result.EuPlus, 10);
            Assert.Equal(136.75, result.EuMinus, 10);
        }

        [Fact]
        public void EuOnly_AbsoluteEu_InTheOtherUnitSystem_IsScaledByTheEuSpanRatio()
        {
            var def = new ToleranceDefinition();
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.AbsoluteEu, Value = 0.45, Unit = "degF", UnitSystem = UnitSystem.English });

            // English span 250, SI span 100 -> ratio 0.4.
            SignalConfig signal = Signal(euHigh: 250, euHighSi: 100);
            ToleranceResult result = _engine.Calculate(50, UnitSystem.Si, signal, def);

            Assert.Equal(0.18, result.Tolerance, 10);
        }

        [Fact]
        public void EuOnly_PercentOfEuSpan_StaysOnTheFastPath()
        {
            ToleranceResult result = _engine.Calculate(
                125, UnitSystem.English, Signal(), Percent(0.003, PercentBasis.EuSpan));

            Assert.True(result.UsedEuFastPath);
            Assert.Equal(0.75, result.Tolerance, 10);
        }

        // --- Path B: linear round-trip -----------------------------------------

        [Fact]
        public void Linear_PercentOfRawSpan_RoundTripsSymmetrically()
        {
            ToleranceResult result = _engine.Calculate(125, UnitSystem.English, Signal(), Percent(0.003));

            Assert.Equal(ToleranceOutcome.Calculated, result.Outcome);
            Assert.False(result.UsedEuFastPath);
            Assert.Equal(12, result.RawExpected, 10);
            Assert.Equal(0.048, result.RawTolerance, 10);
            Assert.Equal(0.75, result.Tolerance, 10);
            Assert.False(result.Extrapolated);
        }

        [Fact]
        public void Linear_Reverse_MirrorsTheEdgesButKeepsTheSameTolerance()
        {
            ToleranceResult result = _engine.Calculate(
                125, UnitSystem.English, Signal(sense: ConversionSense.Reverse), Percent(0.003));

            Assert.Equal(12, result.RawExpected, 10);
            Assert.Equal(0.75, result.Tolerance, 10);
        }

        [Fact]
        public void Linear_MixedRawTerms_AreSummedInRawSpace()
        {
            var def = new ToleranceDefinition { SignalType = "0-10V", ModuleType = "AI-664" };
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.Percent, Value = 0.001, PercentBasis = PercentBasis.RawSpan });
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.AbsoluteRaw, Value = 0.002, Unit = "V" });

            SignalConfig signal = Signal(rawLow: 0, rawHigh: 10, euHigh: 500);
            ToleranceResult result = _engine.Calculate(250, UnitSystem.English, signal, def);

            Assert.Equal(5, result.RawExpected, 10);
            Assert.Equal(0.012, result.RawTolerance, 10);
            Assert.Equal(0.6, result.Tolerance, 10);
        }

        [Fact]
        public void Linear_MixedRawAndEuTerms_AddTheEuTermOutsideTheRoundTrip()
        {
            var def = new ToleranceDefinition();
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.Percent, Value = 0.003, PercentBasis = PercentBasis.RawSpan });
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.AbsoluteEu, Value = 1.0, Unit = "degF", UnitSystem = UnitSystem.English });

            ToleranceResult result = _engine.Calculate(125, UnitSystem.English, Signal(), def);

            // 0.75 from the raw round-trip + 1.0 EU term.
            Assert.Equal(1.75, result.Tolerance, 10);
        }

        // --- Path B: non-linear curves ---------------------------------------

        [Fact]
        public void SquareRoot_ProducesAnAsymmetricBand_LargerOnTheLowSide()
        {
            SignalConfig signal = Signal(scaleType: ScaleTypeNames.SquareRoot, euHigh: 100);
            ToleranceResult result = _engine.Calculate(50, UnitSystem.English, signal, Percent(0.003));

            double EuAt(double rawEdge) => 100.0 * Math.Sqrt((rawEdge - 4) / 16.0);
            double expectedPlus = EuAt(8.048);
            double expectedMinus = EuAt(7.952);

            Assert.Equal(8.0, result.RawExpected, 10);
            Assert.Equal(expectedPlus, result.EuPlus, 9);
            Assert.Equal(expectedMinus, result.EuMinus, 9);
            Assert.Equal(Math.Max(Math.Abs(expectedPlus - 50), Math.Abs(expectedMinus - 50)), result.Tolerance, 9);
            Assert.True(Math.Abs(expectedMinus - 50) > Math.Abs(expectedPlus - 50));
        }

        [Fact]
        public void Logarithmic_MatchesAHandComputedRoundTrip()
        {
            SignalConfig signal = Signal(scaleType: ScaleTypeNames.Logarithmic, euHigh: 100);
            ToleranceResult result = _engine.Calculate(60, UnitSystem.English, signal, Percent(0.002));

            double fwd(double x) => (Math.Pow(10, 2 * x) - 1) / (Math.Pow(10, 2) - 1);
            double inv(double y) => Math.Log10(y * (Math.Pow(10, 2) - 1) + 1) / 2;
            double rawExpected = 4 + fwd(0.60) * 16;
            double rawTol = 0.002 * 16;
            double euPlus = 100 * inv((rawExpected + rawTol - 4) / 16);
            double euMinus = 100 * inv((rawExpected - rawTol - 4) / 16);

            Assert.Equal(rawExpected, result.RawExpected, 9);
            Assert.Equal(Math.Max(Math.Abs(euPlus - 60), Math.Abs(euMinus - 60)), result.Tolerance, 9);
        }

        // --- extrapolation and the curve guard -------------------------------

        [Fact]
        public void Linear_ExtrapolatesCleanlyWhenTheBandRunsPastTheRawRange()
        {
            ToleranceResult result = _engine.Calculate(249, UnitSystem.English, Signal(), Percent(0.05));

            Assert.Equal(ToleranceOutcome.Calculated, result.Outcome);
            Assert.True(result.Extrapolated);
            Assert.Equal(12.5, result.Tolerance, 10);
        }

        [Fact]
        public void SquareRoot_FlagsTheRowWhenTheInverseCurveGoesNonFinite()
        {
            SignalConfig signal = Signal(scaleType: ScaleTypeNames.SquareRoot, euHigh: 100);
            ToleranceResult result = _engine.Calculate(1, UnitSystem.English, signal, Percent(0.05));

            Assert.Equal(ToleranceOutcome.CurveUndefined, result.Outcome);
            Assert.Equal(0, result.Tolerance);
            Assert.NotEqual(0, result.RawExpected);
            Assert.NotNull(result.Message);
        }

        // --- failure outcomes ----------------------------------------------

        [Fact]
        public void NoTerms_ReportsNoToleranceMatch()
        {
            ToleranceResult result = _engine.Calculate(
                10, UnitSystem.English, Signal(), new ToleranceDefinition());

            Assert.Equal(ToleranceOutcome.NoToleranceMatch, result.Outcome);
        }

        [Fact]
        public void NonNumericExpected_ReportsInvalidExpected()
        {
            ToleranceResult result = _engine.Calculate(
                double.NaN, UnitSystem.English, Signal(), Percent(0.003));

            Assert.Equal(ToleranceOutcome.InvalidExpected, result.Outcome);
        }

        [Fact]
        public void UnknownScaleType_ReportsScaleTypeUnknown_WhenTheRoundTripIsNeeded()
        {
            ToleranceResult result = _engine.Calculate(
                125, UnitSystem.English, Signal(scaleType: "Cubed"), Percent(0.003));

            Assert.Equal(ToleranceOutcome.ScaleTypeUnknown, result.Outcome);
        }

        [Fact]
        public void ReverseSenseWithANonLinearCurve_IsRejected()
        {
            SignalConfig signal = Signal(scaleType: ScaleTypeNames.SquareRoot, sense: ConversionSense.Reverse, euHigh: 100);

            ToleranceResult result = _engine.Calculate(50, UnitSystem.English, signal, Percent(0.003));

            Assert.Equal(ToleranceOutcome.InvalidSignalConfig, result.Outcome);
        }

        [Fact]
        public void ZeroWidthEuRange_IsRejectedOnTheRoundTrip()
        {
            SignalConfig signal = Signal(euLow: 100, euHigh: 100);

            ToleranceResult result = _engine.Calculate(100, UnitSystem.English, signal, Percent(0.003));

            Assert.Equal(ToleranceOutcome.InvalidSignalConfig, result.Outcome);
        }
    }
}
