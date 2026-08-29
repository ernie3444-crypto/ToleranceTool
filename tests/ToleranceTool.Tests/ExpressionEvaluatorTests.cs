using System.Collections.Generic;
using ToleranceTool.Core.Expressions;
using Xunit;

namespace ToleranceTool.Tests
{
    public class ExpressionEvaluatorTests
    {
        [Fact]
        public void Evaluate_SubstitutesVariables()
        {
            var evaluator = new ExpressionEvaluator("Pow(x, 2) + offset");

            double value = evaluator.Evaluate(new Dictionary<string, double> { ["x"] = 3, ["offset"] = 1 });

            Assert.Equal(10, value, 10);
        }

        [Fact]
        public void Evaluate_ReusesTheParsedExpressionAcrossCalls()
        {
            var evaluator = new ExpressionEvaluator("Sqrt(x)");

            Assert.Equal(2, evaluator.Evaluate(new Dictionary<string, double> { ["x"] = 4 }), 10);
            Assert.Equal(3, evaluator.Evaluate(new Dictionary<string, double> { ["x"] = 9 }), 10);
        }

        [Fact]
        public void Constructor_ThrowsOnAGarbledBody()
        {
            Assert.Throws<ExpressionException>(() => new ExpressionEvaluator("Pow(x,"));
        }

        [Fact]
        public void Constructor_ThrowsOnAnEmptyBody()
        {
            Assert.Throws<ExpressionException>(() => new ExpressionEvaluator("   "));
        }

        [Fact]
        public void Validate_ReturnsNullForAWellFormedBody_AndAMessageOtherwise()
        {
            Assert.Null(ExpressionEvaluator.Validate("x * 2"));
            Assert.NotNull(ExpressionEvaluator.Validate("x *"));
        }

        [Fact]
        public void Evaluate_ThrowsWhenTheResultIsNotNumeric()
        {
            var evaluator = new ExpressionEvaluator("'a' + 'b'");

            Assert.Throws<ExpressionException>(
                () => evaluator.Evaluate(new Dictionary<string, double>()));
        }
    }
}
