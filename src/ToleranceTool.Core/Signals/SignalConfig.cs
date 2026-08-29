namespace ToleranceTool.Core.Signals
{
    /// <summary>
    /// The full per-signal configuration consumed by the calculation engine.
    /// Assembled by the import layer from one or more sources joined on
    /// <see cref="UniversalId"/>.
    /// </summary>
    public sealed class SignalConfig
    {
        /// <summary>Join key that links records across import sources.</summary>
        public string UniversalId { get; set; } = string.Empty;

        /// <summary>Name matched against a datasheet row's System ID.</summary>
        public string SensorName { get; set; } = string.Empty;

        public ConversionSense ConversionSense { get; set; } = ConversionSense.Direct;

        /// <summary>Name of a curve in the scale-type library.</summary>
        public string ScaleType { get; set; } = ScaleTypeNames.Linear;

        /// <summary>e.g. "4-20mA". First key into the tolerance library and the signal-type registry.</summary>
        public string SignalType { get; set; } = string.Empty;

        /// <summary>The I/O card in use. Second key into the tolerance library.</summary>
        public string ModuleType { get; set; } = string.Empty;

        /// <summary>Raw range low endpoint (from the signal-type registry).</summary>
        public double RawLow { get; set; }

        /// <summary>Raw range high endpoint (from the signal-type registry).</summary>
        public double RawHigh { get; set; }

        /// <summary>EU range low endpoint in display units.</summary>
        public double EuLow { get; set; }

        /// <summary>EU range high endpoint in display units.</summary>
        public double EuHigh { get; set; }

        /// <summary>EU range low endpoint in SI units.</summary>
        public double EuLowSi { get; set; }

        /// <summary>EU range high endpoint in SI units.</summary>
        public double EuHighSi { get; set; }

        public double RawSpan => RawHigh - RawLow;

        public double EuSpan => EuHigh - EuLow;

        public double EuSpanSi => EuHighSi - EuLowSi;

        /// <summary>Returns the EU range endpoints for the given unit system.</summary>
        public (double Low, double High) EuRange(UnitSystem unitSystem) =>
            unitSystem == UnitSystem.Si ? (EuLowSi, EuHighSi) : (EuLow, EuHigh);
    }
}
