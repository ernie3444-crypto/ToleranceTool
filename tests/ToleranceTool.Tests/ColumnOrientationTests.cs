using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Aliases;
using ToleranceTool.Configuration.Datasheet;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Precision;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;
using ToleranceTool.Excel.Datasheet;
using ToleranceTool.Import;
using ToleranceTool.Import.Files;
using Xunit;

namespace ToleranceTool.Tests
{
    public class TransposeTests
    {
        [Fact]
        public void TabularData_Transpose_SwapsRowsAndColumns()
        {
            var data = TabularData.ParseCsv("a,b,c\n1,2,3\n");
            TabularData t = data.Transpose();

            Assert.Equal(3, t.RowCount);
            Assert.Equal("a", t.Cell(0, 0));
            Assert.Equal("1", t.Cell(0, 1));
            Assert.Equal("c", t.Cell(2, 0));
            Assert.Equal("3", t.Cell(2, 1));
        }

        [Fact]
        public void TransposedDatasheet_ReadsAndWritesThroughTheSwap()
        {
            var inner = new FakeDatasheet("S", new[]
            {
                new string?[] { "label", "sigA", "sigB" },
                new string?[] { "10", "11", "12" },
            });

            var t = new TransposedDatasheet(inner);
            Assert.Equal("sigA", t.GetText(1, 0));
            Assert.Equal(11d, t.GetNumber(1, 1));

            t.SetNumber(2, 1, 99);
            Assert.Equal(99d, inner.Written[(1, 2)]);
        }
    }

    public class ColumnOrientedImportTests
    {
        [Fact]
        public void FileSignalSource_ReadsAKeyValueSheet()
        {
            // One signal per column; field labels down column A.
            var data = TabularData.ParseCsv(
                "UniversalId,UT-1,UT-2\n" +
                "SensorName,FT-201,PT-330\n" +
                "ScaleType,Linear,Linear\n");

            var def = new ImportSourceDefinition("kv.csv", SignalSourceKind.DelimitedText, "kv.csv")
            {
                IsMaster = true,
                Orientation = SignalDataOrientation.ColumnPerSignal,
                UniversalIdLocator = "1", // row 1 (1-based) holds the Universal ID
            };
            def.Fields.Add(new FieldBinding(SignalField.SensorName, "2", true));
            def.Fields.Add(new FieldBinding(SignalField.ScaleType, "3", true));

            var records = new FileSignalSource(def, data).Read();

            Assert.Equal(2, records.Count);
            Assert.Equal("UT-1", records[0].UniversalId);
            Assert.Equal("FT-201", records[0].Fields[SignalField.SensorName]);
            Assert.Equal("PT-330", records[1].Fields[SignalField.SensorName]);
        }
    }

    public class ParameterPerRowImportTests
    {
        private static (ImportSourceDefinition Def, TabularData Data) Eav()
        {
            // Unique ID | Parameter | Value | Metric value
            var data = TabularData.ParseCsv(
                "UniqueID,Parameter,Value,MetricValue\n" +
                "53-501-52,Sensor,FT-201,\n" +
                "53-501-52,Sense,Direct,\n" +
                "53-501-52,Scale,Linear,\n" +
                "53-501-52,SigType,4-20mA,\n" +
                "53-501-52,Module,AI-871,\n" +
                "53-501-52,EU_Low,25,50\n" +
                "53-501-52,EU_High,50,72\n" +
                "53-501-52,Ignored,whatever,\n" +
                "53-501-99,Sensor,PT-330,\n" +
                "53-501-99,Sense,Direct,\n" +
                "53-501-99,Scale,Linear,\n" +
                "53-501-99,SigType,4-20mA,\n" +
                "53-501-99,Module,AI-871,\n" +
                "53-501-99,EU_Low,0,-10\n" +
                "53-501-99,EU_High,100,38\n");

            var def = new ImportSourceDefinition("eav.csv", SignalSourceKind.DelimitedText, "eav.csv")
            {
                IsMaster = true,
                Orientation = SignalDataOrientation.ParameterPerRow,
                HeaderRowIndex = 0,
                UniversalIdLocator = "A",
                ParameterNameLocator = "B",
                ParameterValueLocator = "C",
                ParameterMetricLocator = "D",
            };
            def.Fields.Add(new FieldBinding(SignalField.SensorName, "Sensor", true));
            def.Fields.Add(new FieldBinding(SignalField.ConversionSense, "Sense", true));
            def.Fields.Add(new FieldBinding(SignalField.ScaleType, "Scale", true));
            def.Fields.Add(new FieldBinding(SignalField.SignalType, "SigType", true));
            def.Fields.Add(new FieldBinding(SignalField.ModuleType, "Module", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLow, "EU_Low", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHigh, "EU_High", true));
            return (def, data);
        }

        [Fact]
        public void FileSignalSource_GroupsRowsByKey_AndFillsSiFromTheMetricColumn()
        {
            (ImportSourceDefinition def, TabularData data) = Eav();

            var records = new FileSignalSource(def, data).Read();

            Assert.Equal(2, records.Count);
            SignalFieldRecord first = records[0];
            Assert.Equal("53-501-52", first.UniversalId);
            Assert.Equal("FT-201", first.Fields[SignalField.SensorName]);
            Assert.Equal("25", first.Fields[SignalField.EuLow]);
            Assert.Equal("50", first.Fields[SignalField.EuLowSi]);
            Assert.Equal("50", first.Fields[SignalField.EuHigh]);
            Assert.Equal("72", first.Fields[SignalField.EuHighSi]);
            Assert.False(first.Fields.ContainsKey("Ignored"));
        }

        [Fact]
        public void SignalSetBuilder_BuildsCompleteSignalsFromAParameterPerRowSource()
        {
            (ImportSourceDefinition def, TabularData data) = Eav();

            ConfigLoadResult<ResolvedSignalSet> result = new SignalSetBuilder()
                .Add(new FileSignalSource(def, data), def)
                .Build();

            Assert.False(result.HasErrors);
            ResolvedSignalSet set = result.Value;
            Assert.Equal(2, set.Count);
            Assert.True(set.IsReady);

            ResolvedSignal signal = set.Find("53-501-52")!;
            Assert.Equal(ConversionSense.Direct, signal.Config.ConversionSense);
            Assert.Equal("4-20mA", signal.Config.SignalType);
            Assert.Equal(25, signal.Config.EuLow, 6);
            Assert.Equal(72, signal.Config.EuHighSi, 6);
        }
    }

    public class ColumnOrientedRunnerTests
    {
        [Fact]
        public void Runner_HandlesAColumnOrientedDatasheetByTransposing()
        {
            // Parameters down column 0; each further column is a test case.
            var sheet = new FakeDatasheet("Cases", new[]
            {
                new string?[] { "Signal", "FT-201 Flow", "FT-201" },
                new string?[] { "Expected", "125", "125" },
                new string?[] { "Tol", "", "" },
            });

            var mapping = new DatasheetMapping
            {
                Orientation = DatasheetOrientation.ColumnPerCase,
                HeaderRowIndex = 0, // after transpose, the label column becomes row 0
                Precision = PrecisionPolicy.DecimalPlaces(6),
            };
            mapping.Headers[DatasheetParameter.SystemId] = "Signal";
            mapping.Headers[DatasheetParameter.Expected] = "Expected";
            mapping.Headers[DatasheetParameter.Tolerance] = "Tol";

            var signal = new SignalConfig
            {
                UniversalId = "UT-1", SensorName = "FT-201", ScaleType = ScaleTypeNames.Linear,
                SignalType = "4-20mA", ModuleType = "AI-871",
                RawLow = 4, RawHigh = 20, EuLow = 0, EuHigh = 250, EuLowSi = 0, EuHighSi = 250,
            };
            var tolerances = new ToleranceLibrary();
            var def = new ToleranceDefinition { SignalType = "4-20mA", ModuleType = "AI-871" };
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.Percent, Value = 0.003, PercentBasis = PercentBasis.RawSpan });
            tolerances.Add(def);

            var runner = new DatasheetRunner(
                new SignalResolver(new[] { signal }, AliasTableSet.Empty()), tolerances, ScaleCurveLibrary.CreateDefault());

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Apply);

            Assert.True(result.DidRun);
            Assert.Equal(2, result.Written);
            // Tolerance row is index 2; the two case columns are 1 and 2.
            Assert.Equal(0.75, sheet.Written[(2, 1)], 6);
            Assert.Equal(0.75, sheet.Written[(2, 2)], 6);
        }
    }
}
