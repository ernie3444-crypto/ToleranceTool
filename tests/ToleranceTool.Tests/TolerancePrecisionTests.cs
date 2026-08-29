using ToleranceTool.Core.Precision;
using Xunit;

namespace ToleranceTool.Tests
{
    public class TolerancePrecisionTests
    {
        [Theory]
        [InlineData(123.456, 3, 123)]
        [InlineData(0.00481234, 2, 0.0048)]
        [InlineData(0.75, 2, 0.75)]
        [InlineData(9.9987, 3, 10.0)]
        public void RoundToSignificantFigures_KeepsTheRequestedFigureCount(double value, int figures, double expected)
        {
            Assert.Equal(expected, TolerancePrecision.RoundToSignificantFigures(value, figures, RoundingMode.HalfToEven), 10);
        }

        [Fact]
        public void RoundToDecimalPlaces_HonoursTheRoundingMode()
        {
            Assert.Equal(0.12, TolerancePrecision.RoundToDecimalPlaces(0.125, 2, RoundingMode.HalfToEven), 10);
            Assert.Equal(0.13, TolerancePrecision.RoundToDecimalPlaces(0.125, 2, RoundingMode.HalfUp), 10);
        }

        [Fact]
        public void Round_MatchExpected_UsesTheSuppliedDigitCount()
        {
            PrecisionPolicy policy = PrecisionPolicy.MatchExpected();

            Assert.Equal(0.048, TolerancePrecision.Round(0.0481234, policy, expectedSignificantDigits: 2), 10);
        }

        [Fact]
        public void Round_MatchExpected_ReturnsTheValueUnchangedWhenTheDigitCountIsUnknown()
        {
            PrecisionPolicy policy = PrecisionPolicy.MatchExpected();

            Assert.Equal(0.0481234, TolerancePrecision.Round(0.0481234, policy, expectedSignificantDigits: null), 10);
        }

        [Fact]
        public void Round_FixedModes_RoundAsConfigured()
        {
            Assert.Equal(0.75, TolerancePrecision.Round(0.7513, PrecisionPolicy.SignificantFigures(2)), 10);
            Assert.Equal(0.75, TolerancePrecision.Round(0.7513, PrecisionPolicy.DecimalPlaces(2)), 10);
        }

        [Fact]
        public void Round_LeavesNonFiniteValuesAlone()
        {
            Assert.True(double.IsNaN(TolerancePrecision.Round(double.NaN, PrecisionPolicy.SignificantFigures(3))));
        }
    }
}
