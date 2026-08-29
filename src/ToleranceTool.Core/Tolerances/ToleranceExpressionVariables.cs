using System;
using System.Collections.Generic;

namespace ToleranceTool.Core.Tolerances
{
    /// <summary>
    /// The variables a <see cref="ToleranceTermKind.Expression"/> body may reference.
    /// Matches the set the engine supplies in <c>ToleranceEngine.EvaluateExpressionTerm</c>.
    /// </summary>
    public static class ToleranceExpressionVariables
    {
        private static readonly HashSet<string> Names = new HashSet<string>(
            new[]
            {
                "expected",
                "rawExpected",
                "rawLow",
                "rawHigh",
                "rawSpan",
                "euLow",
                "euHigh",
                "euSpan",
            },
            StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> All => Names;

        public static bool IsKnown(string name) => Names.Contains(name);
    }
}
