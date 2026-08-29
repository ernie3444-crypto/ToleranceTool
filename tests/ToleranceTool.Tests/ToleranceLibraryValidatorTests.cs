using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Tolerances;
using Xunit;

namespace ToleranceTool.Tests
{
    public class ToleranceLibraryValidatorTests
    {
        private static ToleranceLibrary WithTerm(ToleranceTerm term)
        {
            var library = new ToleranceLibrary();
            var definition = new ToleranceDefinition { SignalType = "s", ModuleType = "m" };
            definition.Terms.Add(term);
            library.Add(definition);
            return library;
        }

        [Fact]
        public void Flags_APercentValueThatLooksLikeAWholePercentage()
        {
            var issues = ToleranceLibraryValidator.Validate(
                WithTerm(new ToleranceTerm { Kind = ToleranceTermKind.Percent, Value = 3, PercentBasis = PercentBasis.RawSpan }));

            Assert.Contains(issues, i => i.Severity == ConfigSeverity.Warning && i.Message.Contains("fraction"));
        }

        [Fact]
        public void Flags_AnExpressionWithAnUnknownVariable()
        {
            var issues = ToleranceLibraryValidator.Validate(
                WithTerm(new ToleranceTerm { Kind = ToleranceTermKind.Expression, ExpressionBody = "rawExpected * gain", Space = ToleranceSpace.Raw }));

            ConfigIssue issue = Assert.Single(issues.Where(i => i.Severity == ConfigSeverity.Error));
            Assert.Contains("gain", issue.Message);
        }

        [Fact]
        public void Flags_AnUnparseableExpression()
        {
            var issues = ToleranceLibraryValidator.Validate(
                WithTerm(new ToleranceTerm { Kind = ToleranceTermKind.Expression, ExpressionBody = "rawExpected *", Space = ToleranceSpace.Raw }));

            Assert.Contains(issues, i => i.Severity == ConfigSeverity.Error);
        }

        [Fact]
        public void Accepts_AnExpressionOverKnownVariablesOnly()
        {
            var issues = ToleranceLibraryValidator.Validate(
                WithTerm(new ToleranceTerm { Kind = ToleranceTermKind.Expression, ExpressionBody = "Max(rawSpan * 0.001, 0.05)", Space = ToleranceSpace.Raw }));

            Assert.DoesNotContain(issues, i => i.Severity == ConfigSeverity.Error);
        }

        [Fact]
        public void Flags_AnAbsoluteTermWithNoUnit()
        {
            var issues = ToleranceLibraryValidator.Validate(
                WithTerm(new ToleranceTerm { Kind = ToleranceTermKind.AbsoluteEu, Value = 0.5 }));

            Assert.Contains(issues, i => i.Message.Contains("no unit"));
        }
    }
}
