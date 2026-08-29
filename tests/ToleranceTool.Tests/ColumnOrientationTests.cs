using System.Collections.Generic;
using System.Linq;
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
