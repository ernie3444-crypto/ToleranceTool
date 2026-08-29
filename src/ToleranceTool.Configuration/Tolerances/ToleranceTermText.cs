using System.Globalization;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.Configuration.Tolerances
{
    /// <summary>Short human-readable descriptions of tolerance terms and definitions, for the editor and summaries.</summary>
    public static class ToleranceTermText
    {
        public static string Describe(ToleranceTerm term)
        {
            switch (term.Kind)
            {
                case ToleranceTermKind.Percent:
                    return $"{term.Value * 100:0.####}% of {BasisText(term.PercentBasis)}";

                case ToleranceTermKind.AbsoluteEu:
                    return $"±{Number(term.Value)} {Unit(term.Unit)} ({term.UnitSystem} EU)";

                case ToleranceTermKind.AbsoluteRaw:
                    return $"±{Number(term.Value)} {Unit(term.Unit)} (raw)";

                case ToleranceTermKind.Expression:
                    return $"expr [{(term.Space == ToleranceSpace.Eu ? "EU" : "raw")}]: {term.ExpressionBody}";

                default:
                    return term.Kind.ToString();
            }
        }

        public static string DescribeDefinition(ToleranceDefinition definition)
        {
            string[] parts = new string[definition.Terms.Count];
            for (int i = 0; i < definition.Terms.Count; i++)
            {
                parts[i] = Describe(definition.Terms[i]);
            }

            string terms = parts.Length == 0 ? "(no terms)" : string.Join("  +  ", parts);
            string path = definition.IsEuOnly ? "EU fast path" : "raw round-trip";
            return $"{terms}   —   {path}";
        }

        private static string BasisText(PercentBasis basis)
        {
            switch (basis)
            {
                case PercentBasis.RawSpan: return "raw span";
                case PercentBasis.EuSpan: return "EU span";
                case PercentBasis.Reading: return "reading";
                default: return basis.ToString();
            }
        }

        private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Unit(string unit) => string.IsNullOrWhiteSpace(unit) ? "?" : unit;
    }
}
