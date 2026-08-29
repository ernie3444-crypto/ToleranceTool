namespace ToleranceTool.Core.Tolerances
{
    public enum ToleranceTermKind
    {
        /// <summary>A fraction of a span or of the reading.</summary>
        Percent = 0,

        /// <summary>A fixed magnitude already in engineering units.</summary>
        AbsoluteEu = 1,

        /// <summary>A fixed magnitude in raw units.</summary>
        AbsoluteRaw = 2,

        /// <summary>An NCalc expression.</summary>
        Expression = 3,
    }

    /// <summary>What a <see cref="ToleranceTermKind.Percent"/> term is a percentage of.</summary>
    public enum PercentBasis
    {
        /// <summary>Fraction of the raw span (default). For 4-20 mA this is a fraction of 16.</summary>
        RawSpan = 0,

        /// <summary>Fraction of the EU span, in the row's unit system.</summary>
        EuSpan = 1,

        /// <summary>Fraction of the reading (the expected value).</summary>
        Reading = 2,
    }

    /// <summary>The space a term's magnitude lives in.</summary>
    public enum ToleranceSpace
    {
        Raw = 0,
        Eu = 1,
    }

    /// <summary>
    /// One additive term of a tolerance band. See the Tolerance library section
    /// of the architecture doc for the full vocabulary.
    /// </summary>
    public sealed class ToleranceTerm
    {
        public ToleranceTermKind Kind { get; set; }

        /// <summary>Numeric value: a fraction for <see cref="ToleranceTermKind.Percent"/>, else a magnitude.</summary>
        public double Value { get; set; }

        public PercentBasis PercentBasis { get; set; } = PercentBasis.RawSpan;

        /// <summary>Unit label for the Absolute* kinds.</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>Unit system an <see cref="ToleranceTermKind.AbsoluteEu"/> value is expressed in.</summary>
        public Signals.UnitSystem UnitSystem { get; set; } = Signals.UnitSystem.English;

        /// <summary>NCalc body for <see cref="ToleranceTermKind.Expression"/>.</summary>
        public string ExpressionBody { get; set; } = string.Empty;

        /// <summary>Explicit space for Percent/Expression terms.</summary>
        public ToleranceSpace Space { get; set; } = ToleranceSpace.Raw;

        /// <summary>True when this term is resolved entirely in EU and needs no raw round-trip.</summary>
        public bool IsEuSpace =>
            Kind == ToleranceTermKind.AbsoluteEu
            || (Kind == ToleranceTermKind.Percent && PercentBasis == PercentBasis.EuSpan)
            || (Kind == ToleranceTermKind.Percent && Space == ToleranceSpace.Eu)
            || (Kind == ToleranceTermKind.Expression && Space == ToleranceSpace.Eu);
    }
}
