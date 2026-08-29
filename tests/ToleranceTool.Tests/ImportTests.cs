using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Core.Signals;
using ToleranceTool.Import;
using ToleranceTool.Import.Files;
using Xunit;

namespace ToleranceTool.Tests
{
    public class ColumnRefTests
    {
        [Theory]
        [InlineData("A", 0)]
        [InlineData("Z", 25)]
        [InlineData("AA", 26)]
        [InlineData("AW", 48)]
        public void ToIndex_MatchesExcel(string letter, int index)
        {
            Assert.Equal(index, ColumnRef.ToIndex(letter));
            Assert.Equal(letter, ColumnRef.FromIndex(index));
        }

        [Theory]
        [InlineData("C", 2)]
        [InlineData("3", 2)]
        public void TryParse_AcceptsLettersAndOneBasedNumbers(string reference, int index)
        {
            Assert.True(ColumnRef.TryParse(reference, out int parsed));
            Assert.Equal(index, parsed);
        }
    }

    public class CsvParsingTests
    {
        [Fact]
        public void ParseCsv_HandlesQuotesEmbeddedCommasNewlinesAndBlanks()
        {
            var data = TabularData.ParseCsv("a,b,c\r\n1,\"x, y\",\"line\r\nbreak\"\r\n2,,z\r\n");

            Assert.Equal(3, data.RowCount);
            Assert.Equal("x, y", data.Cell(1, 1));
            Assert.Equal("line\r\nbreak", data.Cell(1, 2));
            Assert.Null(data.Cell(2, 1));
            Assert.Equal("z", data.Cell(2, 2));
        }
    }

    public class FileSignalSourceTests
    {
        private static (ImportSourceDefinition Def, TabularData Data) Master()
        {
            var data = TabularData.ParseCsv(
                "UID,Sensor,Sense,Scale,Signal,Module\n" +
                "UT-1001,FT-201 Flow,Direct,SquareRoot,4-20mA,AI-871\n" +
                "UT-1002,PT-330 Press,Direct,Linear,4-20mA,AI-871\n" +
                ",skipme,Direct,Linear,4-20mA,AI-871\n");

            var def = new ImportSourceDefinition("master.csv", SignalSourceKind.DelimitedText, "master.csv")
            {
                IsMaster = true,
                UniversalIdLocator = "A",
            };
            def.Fields.Add(new FieldBinding(SignalField.SensorName, "B", true));
            def.Fields.Add(new FieldBinding(SignalField.ConversionSense, "C", true));
            def.Fields.Add(new FieldBinding(SignalField.ScaleType, "D", true));
            def.Fields.Add(new FieldBinding(SignalField.SignalType, "E", true));
            def.Fields.Add(new FieldBinding(SignalField.ModuleType, "F", true));
            return (def, data);
        }

        private static (ImportSourceDefinition Def, TabularData Data) Ranges()
        {
            var data = TabularData.ParseCsv(
                "UID,Lo,Hi,LoSI,HiSI\n" +
                "UT-1001,0,250,0,0.0158\n" +
                "UT-1002,0,100,0,689.5\n");

            var def = new ImportSourceDefinition("ranges.csv", SignalSourceKind.DelimitedText, "ranges.csv")
            {
                UniversalIdLocator = "A",
            };
            def.Fields.Add(new FieldBinding(SignalField.EuLow, "B", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHigh, "C", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLowSi, "D", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHighSi, "E", true));
            return (def, data);
        }

        [Fact]
        public void Read_SkipsBlankKeyRows_AndTrims()
        {
            (ImportSourceDefinition def, TabularData data) = Master();

            var records = new FileSignalSource(def, data).Read();

            Assert.Equal(2, records.Count);
            Assert.Equal("UT-1001", records[0].UniversalId);
            Assert.Equal("FT-201 Flow", records[0].Fields[SignalField.SensorName]);
        }

        [Fact]
        public void Build_LeftJoinsSourcesOnUniversalId_AndMapsSignalConfig()
        {
            (ImportSourceDefinition masterDef, TabularData masterData) = Master();
            (ImportSourceDefinition rangeDef, TabularData rangeData) = Ranges();

            ConfigLoadResult<ResolvedSignalSet> result = new SignalSetBuilder()
                .Add(new FileSignalSource(masterDef, masterData), masterDef)
                .Add(new FileSignalSource(rangeDef, rangeData), rangeDef)
                .Build();

            Assert.False(result.HasErrors);
            ResolvedSignalSet set = result.Value;
            Assert.Equal(2, set.Count);
            Assert.True(set.IsReady);

            ResolvedSignal first = set.Find("UT-1001")!;
            Assert.True(first.IsComplete);
            Assert.Equal("FT-201 Flow", first.Config.SensorName);
            Assert.Equal(ConversionSense.Direct, first.Config.ConversionSense);
            Assert.Equal("SquareRoot", first.Config.ScaleType);
            Assert.Equal(250, first.Config.EuHigh, 10);
            Assert.Equal(0.0158, first.Config.EuHighSi, 10);
        }

        [Fact]
        public void Build_FlagsSignalsMissingARequiredField()
        {
            (ImportSourceDefinition masterDef, TabularData masterData) = Master();
            (ImportSourceDefinition rangeDef, TabularData rangeData) = Ranges();

            // Drop UT-1002's ranges row.
            var trimmed = TabularData.ParseCsv("UID,Lo,Hi,LoSI,HiSI\nUT-1001,0,250,0,0.0158\n");

            ConfigLoadResult<ResolvedSignalSet> result = new SignalSetBuilder()
                .Add(new FileSignalSource(masterDef, masterData), masterDef)
                .Add(new FileSignalSource(rangeDef, trimmed), rangeDef)
                .Build();

            ResolvedSignalSet set = result.Value;
            Assert.False(set.IsReady);

            ResolvedSignal second = set.Find("UT-1002")!;
            Assert.False(second.IsComplete);
            Assert.Contains(second.Gaps, g => g.Field == SignalField.EuLow);
        }

        [Fact]
        public void Build_ReportsANonNumericRangeValue()
        {
            (ImportSourceDefinition masterDef, TabularData masterData) = Master();
            (ImportSourceDefinition rangeDef, _) = Ranges();
            var bad = TabularData.ParseCsv("UID,Lo,Hi,LoSI,HiSI\nUT-1001,zero,250,0,0.0158\nUT-1002,0,100,0,689.5\n");

            ConfigLoadResult<ResolvedSignalSet> result = new SignalSetBuilder()
                .Add(new FileSignalSource(masterDef, masterData), masterDef)
                .Add(new FileSignalSource(rangeDef, bad), rangeDef)
                .Build();

            ResolvedSignal first = result.Value.Find("UT-1001")!;
            Assert.Contains(first.Gaps, g => g.Field == SignalField.EuLow && g.Reason.Contains("not a number"));
        }

        [Fact]
        public void Build_RequiresExactlyOneMaster()
        {
            (ImportSourceDefinition masterDef, TabularData masterData) = Master();
            (ImportSourceDefinition rangeDef, TabularData rangeData) = Ranges();

            ConfigLoadResult<ResolvedSignalSet> noMaster = new SignalSetBuilder()
                .Add(new FileSignalSource(rangeDef, rangeData), rangeDef)
                .Build();
            Assert.True(noMaster.HasErrors);

            rangeDef.IsMaster = true;
            ConfigLoadResult<ResolvedSignalSet> twoMasters = new SignalSetBuilder()
                .Add(new FileSignalSource(masterDef, masterData), masterDef)
                .Add(new FileSignalSource(rangeDef, rangeData), rangeDef)
                .Build();
            Assert.True(twoMasters.HasErrors);
        }

        [Fact]
        public void RequiredTolerances_ListsDistinctSignalModulePairs()
        {
            (ImportSourceDefinition masterDef, TabularData masterData) = Master();
            (ImportSourceDefinition rangeDef, TabularData rangeData) = Ranges();

            ResolvedSignalSet set = new SignalSetBuilder()
                .Add(new FileSignalSource(masterDef, masterData), masterDef)
                .Add(new FileSignalSource(rangeDef, rangeData), rangeDef)
                .Build().Value;

            var pairs = set.RequiredTolerances().ToList();
            Assert.Single(pairs);
            Assert.Equal(("4-20mA", "AI-871"), pairs[0]);
        }
    }
}
