using System.IO;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;
using Xunit;

namespace ToleranceTool.Tests
{
    public class ToleranceLibraryXmlTests
    {
        private const string Sample = @"<Tolerances>
  <Tolerance signalType='4-20mA' moduleType='AI-871'>
    <Percent value='0.003' />
  </Tolerance>
  <Tolerance signalType='RTD-PT100' moduleType='AI-664'>
    <AbsoluteEu value='0.45' unit='degF' unitSystem='English' />
  </Tolerance>
  <Tolerance signalType='0-10V' moduleType='AI-664'>
    <Percent value='0.001' basis='rawSpan' />
    <AbsoluteRaw value='0.002' unit='V' />
    <Expression space='eu'>expected * 0.0001</Expression>
  </Tolerance>
</Tolerances>";

        private static ToleranceLibrary Load(string xml)
        {
            ConfigLoadResult<ToleranceLibrary> result = ToleranceLibraryXml.Load(new StringReader(xml));
            Assert.False(result.HasErrors, string.Join("; ", result.Issues.Select(i => i.ToString())));
            return result.Value;
        }

        [Fact]
        public void Load_ReadsEveryTermKind()
        {
            ToleranceLibrary library = Load(Sample);

            Assert.Equal(3, library.Count);

            Assert.True(library.TryGet("4-20mA", "AI-871", out ToleranceDefinition loop));
            ToleranceTerm percent = Assert.Single(loop.Terms);
            Assert.Equal(ToleranceTermKind.Percent, percent.Kind);
            Assert.Equal(0.003, percent.Value, 10);
            Assert.Equal(PercentBasis.RawSpan, percent.PercentBasis);

            Assert.True(library.TryGet("0-10V", "AI-664", out ToleranceDefinition composite));
            Assert.Equal(3, composite.Terms.Count);
            Assert.Equal(ToleranceTermKind.AbsoluteRaw, composite.Terms[1].Kind);
            Assert.Equal("V", composite.Terms[1].Unit);
            Assert.Equal(ToleranceTermKind.Expression, composite.Terms[2].Kind);
            Assert.Equal(ToleranceSpace.Eu, composite.Terms[2].Space);
            Assert.Equal("expected * 0.0001", composite.Terms[2].ExpressionBody);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsEquivalently()
        {
            ToleranceLibrary original = Load(Sample);

            var buffer = new StringWriter();
            ToleranceLibraryXml.Save(original, buffer);
            ToleranceLibrary reloaded = Load(buffer.ToString());

            Assert.Equal(original.Count, reloaded.Count);
            foreach (ToleranceDefinition definition in original.Definitions)
            {
                Assert.True(reloaded.TryGet(definition.SignalType, definition.ModuleType, out ToleranceDefinition other));
                Assert.Equal(definition.Terms.Count, other.Terms.Count);
                for (int i = 0; i < definition.Terms.Count; i++)
                {
                    Assert.Equal(definition.Terms[i].Kind, other.Terms[i].Kind);
                    Assert.Equal(definition.Terms[i].Value, other.Terms[i].Value, 12);
                    Assert.Equal(definition.Terms[i].PercentBasis, other.Terms[i].PercentBasis);
                    Assert.Equal(definition.Terms[i].Space, other.Terms[i].Space);
                    Assert.Equal(definition.Terms[i].Unit, other.Terms[i].Unit);
                    Assert.Equal(definition.Terms[i].UnitSystem, other.Terms[i].UnitSystem);
                    Assert.Equal(definition.Terms[i].ExpressionBody, other.Terms[i].ExpressionBody);
                }
            }
        }

        [Fact]
        public void Load_ReportsMalformedXml()
        {
            ConfigLoadResult<ToleranceLibrary> result = ToleranceLibraryXml.Load(new StringReader("<Tolerances><oops"));
            Assert.True(result.HasErrors);
        }

        [Fact]
        public void Load_ReportsAnUnknownTermElement()
        {
            ConfigLoadResult<ToleranceLibrary> result = ToleranceLibraryXml.Load(new StringReader(
                "<Tolerances><Tolerance signalType='a' moduleType='b'><Wobble value='1'/></Tolerance></Tolerances>"));

            Assert.Contains(result.Issues, i => i.Message.Contains("Wobble"));
        }

        [Fact]
        public void Load_ReportsANonNumericValue()
        {
            ConfigLoadResult<ToleranceLibrary> result = ToleranceLibraryXml.Load(new StringReader(
                "<Tolerances><Tolerance signalType='a' moduleType='b'><Percent value='lots'/></Tolerance></Tolerances>"));

            Assert.True(result.HasErrors);
        }

        [Fact]
        public void Load_ReportsDuplicateKeys()
        {
            ConfigLoadResult<ToleranceLibrary> result = ToleranceLibraryXml.Load(new StringReader(
                "<Tolerances>" +
                "<Tolerance signalType='a' moduleType='b'><Percent value='0.01'/></Tolerance>" +
                "<Tolerance signalType='A' moduleType='B'><Percent value='0.02'/></Tolerance>" +
                "</Tolerances>"));

            Assert.Contains(result.Issues, i => i.Message.Contains("more than one"));
        }

        [Fact]
        public void MissingFor_ListsUndefinedSignalModulePairs()
        {
            ToleranceLibrary library = Load(Sample);

            var required = new[]
            {
                ("4-20mA", "AI-871"),
                ("Tach-Pulse", "CI-220"),
            };

            var missing = library.MissingFor(required);
            Assert.Single(missing);
            Assert.Equal(("Tach-Pulse", "CI-220"), missing[0]);
        }
    }
}
