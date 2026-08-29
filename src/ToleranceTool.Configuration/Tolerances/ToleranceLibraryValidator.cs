using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Core.Expressions;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.Configuration.Tolerances
{
    /// <summary>
    /// Semantic checks over a loaded library: expression bodies parse and only
    /// reference known variables, percentages look like fractions, absolute terms
    /// carry a unit. Structural problems are already caught by the loader.
    /// </summary>
    public static class ToleranceLibraryValidator
    {
        public static IReadOnlyList<ConfigIssue> Validate(ToleranceLibrary library)
        {
            var issues = new List<ConfigIssue>();

            foreach (ToleranceDefinition definition in library.Definitions)
            {
                string scope = ToleranceLibrary.KeyOf(definition);

                for (int i = 0; i < definition.Terms.Count; i++)
                {
                    ValidateTerm(definition.Terms[i], i + 1, scope, issues);
                }
            }

            return issues;
        }

        public static IReadOnlyList<ConfigIssue> ValidateTerm(ToleranceTerm term)
        {
            var issues = new List<ConfigIssue>();
            ValidateTerm(term, 1, null, issues);
            return issues;
        }

        private static void ValidateTerm(ToleranceTerm term, int ordinal, string? scope, List<ConfigIssue> issues)
        {
            string where = scope == null ? $"term {ordinal}" : $"{scope}, term {ordinal}";

            switch (term.Kind)
            {
                case ToleranceTermKind.Percent:
                    if (term.Value < 0)
                    {
                        issues.Add(ConfigIssue.Warning(
                            "A percent term is negative; the band magnitude is always taken as positive.", where));
                    }

                    if (term.PercentBasis != PercentBasis.Reading && term.Value > 1)
                    {
                        issues.Add(ConfigIssue.Warning(
                            $"A percent term is {term.Value} — that is {term.Value * 100:0.#}% of the span. " +
                            "Percentages are fractions (0.3% is 0.003).",
                            where));
                    }

                    break;

                case ToleranceTermKind.AbsoluteEu:
                case ToleranceTermKind.AbsoluteRaw:
                    if (string.IsNullOrWhiteSpace(term.Unit))
                    {
                        issues.Add(ConfigIssue.Warning("An absolute term has no unit label.", where));
                    }

                    if (term.Value == 0)
                    {
                        issues.Add(ConfigIssue.Warning("An absolute term is zero and contributes nothing.", where));
                    }

                    break;

                case ToleranceTermKind.Expression:
                    ValidateExpression(term.ExpressionBody, where, issues);
                    break;
            }
        }

        private static void ValidateExpression(string body, string where, List<ConfigIssue> issues)
        {
            string? parseError = ExpressionEvaluator.Validate(body);
            if (parseError != null)
            {
                issues.Add(ConfigIssue.Error(parseError, where));
                return;
            }

            var evaluator = new ExpressionEvaluator(body);
            List<string> unknown = evaluator.ReferencedVariables()
                .Where(name => !ToleranceExpressionVariables.IsKnown(name))
                .ToList();

            if (unknown.Count > 0)
            {
                issues.Add(ConfigIssue.Error(
                    $"The expression references unknown variable(s): {string.Join(", ", unknown)}. " +
                    $"Available: {string.Join(", ", ToleranceExpressionVariables.All)}.",
                    where));
            }
        }
    }
}
