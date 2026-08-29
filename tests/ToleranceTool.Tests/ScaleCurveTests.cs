using System;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using Xunit;

namespace ToleranceTool.Tests
{
    public class ScaleCurveTests
    {
        [Theory]
        [InlineData(ScaleTypeNames.Linear)]
        [InlineData(ScaleTypeNames.SquareRoot)]
        [InlineData(ScaleTypeNames.Logarithmic)]
        public void BuiltInCurves_SatisfyTheLibraryContract(string name)
        {
            Assert.True(ScaleCurveLibrary.CreateDefault().TryGet(name, out ScaleCurve curve));

            Assert.Empty(curve.Validate());
            Assert.Equal(0, curve.Forward(0), 9);
            Assert.Equal(1, curve.Forward(1), 9);
        }

        [Fact]
        public void SquareRoot_RoundTripsThroughForwardThenInverse()
        {
            ScaleCurveLibrary.CreateDefault().TryGet(ScaleTypeNames.SquareRoot, out ScaleCurve curve);

            double raw = curve.Forward(0.3);
            Assert.Equal(0.09, raw, 9);
            Assert.Equal(0.3, curve.Inverse(raw), 9);
        }

        [Fact]
        public void Logarithmic_MatchesTheClosedForm()
        {
            ScaleCurveLibrary.CreateDefault().TryGet(ScaleTypeNames.Logarithmic, out ScaleCurve curve);

            double x = 0.4;
            double expected = (Math.Pow(10, 2 * x) - 1) / (Math.Pow(10, 2) - 1);
            Assert.Equal(expected, curve.Forward(x), 9);
            Assert.Equal(x, curve.Inverse(curve.Forward(x)), 9);
        }

        [Fact]
        public void Inverse_GoesNonFiniteOutsideTheSquareRootDomain()
        {
            ScaleCurveLibrary.CreateDefault().TryGet(ScaleTypeNames.SquareRoot, out ScaleCurve curve);

            double value = curve.Inverse(-0.05);
            Assert.True(double.IsNaN(value) || double.IsInfinity(value));
        }

        [Fact]
        public void Validate_FlagsACurveThatMissesTheEndpoints()
        {
            var curve = new ScaleCurve(new ScaleType { Name = "Shifted", Forward = "x + 0.1", Inverse = "x - 0.1" });

            Assert.NotEmpty(curve.Validate());
        }

        [Fact]
        public void Validate_FlagsANonMonotonicCurve()
        {
            var curve = new ScaleCurve(new ScaleType { Name = "Dip", Forward = "Pow(x, 2) * 2 - x", Inverse = "x" });

            Assert.Contains(curve.Validate(), p => p.IndexOf("monotonic", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void Library_MatchesNamesCaseInsensitively_AndReportsMisses()
        {
            ScaleCurveLibrary library = ScaleCurveLibrary.CreateDefault();

            Assert.True(library.TryGet("linear", out _));
            Assert.False(library.TryGet("does-not-exist", out _));
            Assert.False(library.TryGet(null!, out _));
        }
    }
}
