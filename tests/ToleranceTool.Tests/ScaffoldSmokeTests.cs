using ToleranceTool.Configuration;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;
using Xunit;

namespace ToleranceTool.Tests
{
    /// <summary>
    /// P0 smoke tests: the solution wires together and the domain model is usable.
    /// Real engine tests arrive with P1.
    /// </summary>
    public class ScaffoldSmokeTests
    {
        [Fact]
        public void SignalConfig_EuRange_SelectsBySystem()
        {
            var signal = new SignalConfig
            {
                EuLow = 0,
                EuHigh = 250,
                EuLowSi = 0,
                EuHighSi = 0.0158,
            };

            Assert.Equal((0, 250), signal.EuRange(UnitSystem.English));
            Assert.Equal((0, 0.0158), signal.EuRange(UnitSystem.Si));
        }

        [Fact]
        public void ToleranceDefinition_IsEuOnly_WhenEveryTermIsEuSpace()
        {
            var def = new ToleranceDefinition { SignalType = "RTD-PT100", ModuleType = "AI-664" };
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.AbsoluteEu, Value = 0.45, Unit = "degF" });

            Assert.True(def.IsEuOnly);

            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.Percent, PercentBasis = PercentBasis.RawSpan, Value = 0.003 });

            Assert.False(def.IsEuOnly);
        }

        [Fact]
        public void ConfigurationPaths_RootFolder_EndsWithToleranceTool()
        {
            Assert.EndsWith(ConfigurationPaths.FolderName, ConfigurationPaths.RootFolder);
        }
    }
}
