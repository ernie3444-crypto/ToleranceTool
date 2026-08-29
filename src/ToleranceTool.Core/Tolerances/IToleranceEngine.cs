using ToleranceTool.Core.Signals;

namespace ToleranceTool.Core.Tolerances
{
    /// <summary>
    /// Calculates the ± tolerance for one expected value. Implemented in P1.
    /// </summary>
    public interface IToleranceEngine
    {
        ToleranceResult Calculate(
            double expected,
            UnitSystem unitSystem,
            SignalConfig signal,
            ToleranceDefinition tolerance);
    }
}
