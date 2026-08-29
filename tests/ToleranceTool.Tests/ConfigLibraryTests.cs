using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Scales;
using ToleranceTool.Configuration.SignalTypes;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Import;
using ToleranceTool.Import.Files;
using Xunit;

namespace ToleranceTool.Tests
{
    public class SignalTypeRegistryXmlTests
    {
        private const string Sample = @"<SignalTypes>
  <SignalType name='4-20mA' rawLow='4' rawHigh='20' unit='mA' />
  <SignalType name='0-10V' rawLow='0' rawHigh='10' unit='V' />
</SignalTypes>";

        [Fact]
        public void Load_ReadsSpecs()
        {
            ConfigLoadResult<SignalTypeRegistry> result = SignalTypeRegistryXml.Load(new StringReader(Sample));

            Assert.False(result.HasErrors);
            Assert.True(result.Value.TryGet("4-20mA", out SignalTypeSpec spec));
            Assert.Equal(4, spec.RawLow, 10);
            Assert.Equal(20, spec.RawHigh, 10);
            Assert.Equal("mA", spec.Unit);
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            SignalTypeRegistry original = SignalTypeRegistryXml.Load(new StringReader(Sample)).Value;
            var buffer = new StringWriter();
            SignalTypeRegistryXml.Save(original, buffer);

            SignalTypeRegistry reloaded = SignalTypeRegistryXml.Load(new StringReader(buffer.ToString())).Value;
            Assert.Equal(original.Count, reloaded.Count);
            Assert.True(reloaded.TryGet("0-10V", out SignalTypeSpec v));
            Assert.Equal(10, v.RawHigh, 10);
        }

        [Fact]
        public void Load_ReportsDuplicatesAndBadNumbers()
        {
            Assert.True(SignalTypeRegistryXml.Load(new StringReader(
                "<SignalTypes><SignalType name='x' rawLow='a' rawHigh='2'/></SignalTypes>")).HasErrors);

            Assert.True(SignalTypeRegistryXml.Load(new StringReader(
                "<SignalTypes><SignalType name='x' rawLow='0' rawHigh='2'/><SignalType name='X' rawLow='0' rawHigh='3'/></SignalTypes>")).HasErrors);
        }
    }

    public class ScaleTypeLibraryXmlTests
    {
        private const string Sample = @"<ScaleTypes>
  <ScaleType name='Linear'><Forward>x</Forward><Inverse>x</Inverse></ScaleType>
  <ScaleType name='SquareRoot'><Forward>Pow(x, 2)</Forward><Inverse>Sqrt(x)</Inverse></ScaleType>
  <ScaleType name='Logarithmic'>
    <Param name='decades' value='2' />
    <Forward>(Pow(10, x * decades) - 1) / (Pow(10, decades) - 1)</Forward>
    <Inverse>Log10(x * (Pow(10, decades) - 1) + 1) / decades</Inverse>
  </ScaleType>
</ScaleTypes>";

        [Fact]
        public void Load_ReadsAndValidatesTheBuiltInCurves()
        {
            ConfigLoadResult<List<ScaleType>> result = ScaleTypeLibraryXml.Load(new StringReader(Sample));

            Assert.False(result.HasErrors);
            Assert.Equal(3, result.Value.Count);

            ScaleCurveLibrary library = ScaleCurveLibrary.From(result.Value);
            Assert.True(library.TryGet("Logarithmic", out ScaleCurve log));
            Assert.Equal(0, log.Forward(0), 9);
            Assert.Equal(1, log.Forward(1), 9);
        }

        [Fact]
        public void Load_FlagsACurveThatBreaksTheContract()
        {
            ConfigLoadResult<List<ScaleType>> result = ScaleTypeLibraryXml.Load(new StringReader(
                "<ScaleTypes><ScaleType name='Bad'><Forward>x + 0.2</Forward><Inverse>x - 0.2</Inverse></ScaleType></ScaleTypes>"));

            Assert.True(result.HasErrors);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsIncludingParameters()
        {
            List<ScaleType> original = ScaleTypeLibraryXml.Load(new StringReader(Sample)).Value;
            var buffer = new StringWriter();
            ScaleTypeLibraryXml.Save(original, buffer);

            List<ScaleType> reloaded = ScaleTypeLibraryXml.Load(new StringReader(buffer.ToString())).Value;
            ScaleType log = reloaded.Single(s => s.Name == "Logarithmic");
            Assert.Equal(2, log.Parameters["decades"], 10);
        }
    }

    public class RegistryJoinTests
    {
        [Fact]
        public void SignalSetBuilder_FillsRawRangeFromTheRegistryWhenImportOmitsIt()
        {
            var master = TabularData.ParseCsv(
                "UID,Sensor,Sense,Scale,Signal,Module,Lo,Hi,LoSI,HiSI\n" +
                "UT-1,S1,Direct,Linear,4-20mA,AI-871,0,100,0,37.8\n");

            var def = new ImportSourceDefinition("m.csv", SignalSourceKind.DelimitedText, "m.csv")
            {
                IsMaster = true,
                UniversalIdLocator = "A",
            };
            def.Fields.Add(new FieldBinding(SignalField.SensorName, "B", true));
            def.Fields.Add(new FieldBinding(SignalField.ConversionSense, "C", true));
            def.Fields.Add(new FieldBinding(SignalField.ScaleType, "D", true));
            def.Fields.Add(new FieldBinding(SignalField.SignalType, "E", true));
            def.Fields.Add(new FieldBinding(SignalField.ModuleType, "F", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLow, "G", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHigh, "H", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLowSi, "I", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHighSi, "J", true));

            var registry = new SignalTypeRegistry();
            registry.Add(new SignalTypeSpec { Name = "4-20mA", RawLow = 4, RawHigh = 20, Unit = "mA" });

            ResolvedSignalSet set = new SignalSetBuilder()
                .WithRegistry(registry)
                .Add(new FileSignalSource(def, master), def)
                .Build().Value;

            ResolvedSignal signal = set.Find("UT-1")!;
            Assert.True(signal.IsComplete);
            Assert.Equal(4, signal.Config.RawLow, 10);
            Assert.Equal(20, signal.Config.RawHigh, 10);
        }

        [Fact]
        public void SignalSetBuilder_FlagsAnUnknownSignalTypeWhenRegistryIsUsedAndImportOmitsTheRange()
        {
            var master = TabularData.ParseCsv(
                "UID,Sensor,Sense,Scale,Signal,Module,Lo,Hi,LoSI,HiSI\n" +
                "UT-1,S1,Direct,Linear,Exotic,AI-871,0,100,0,37.8\n");

            var def = new ImportSourceDefinition("m.csv", SignalSourceKind.DelimitedText, "m.csv") { IsMaster = true, UniversalIdLocator = "A" };
            def.Fields.Add(new FieldBinding(SignalField.SensorName, "B", true));
            def.Fields.Add(new FieldBinding(SignalField.ConversionSense, "C", true));
            def.Fields.Add(new FieldBinding(SignalField.ScaleType, "D", true));
            def.Fields.Add(new FieldBinding(SignalField.SignalType, "E", true));
            def.Fields.Add(new FieldBinding(SignalField.ModuleType, "F", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLow, "G", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHigh, "H", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLowSi, "I", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHighSi, "J", true));

            ResolvedSignalSet set = new SignalSetBuilder()
                .WithRegistry(new SignalTypeRegistry())
                .Add(new FileSignalSource(def, master), def)
                .Build().Value;

            Assert.Contains(set.Find("UT-1")!.Gaps, g => g.Reason.Contains("registry"));
        }
    }
}
