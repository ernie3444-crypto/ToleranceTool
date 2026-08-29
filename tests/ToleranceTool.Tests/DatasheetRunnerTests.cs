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
using Xunit;

namespace ToleranceTool.Tests
{
    public class DatasheetRunnerTests
    {
        private static (FakeDatasheet Sheet, DatasheetMapping Mapping, DatasheetRunner Runner) Setup(
            IEnumerable<string?[]> rows, PrecisionPolicy? precision = null)
        {
            var sheet = new FakeDatasheet("Test", rows);

            var mapping = new DatasheetMapping { HeaderRowIndex = 0, Precision = precision ?? PrecisionPolicy.DecimalPlaces(6) };
            mapping.Headers[DatasheetParameter.SystemId] = "Signal";
            mapping.Headers[DatasheetParameter.Expected] = "Expected";
            mapping.Headers[DatasheetParameter.Tolerance] = "Tol";

            var signals = new List<SignalConfig>
            {
                new SignalConfig
                {
                    UniversalId = "UT-1", SensorName = "FT-201", ScaleType = ScaleTypeNames.Linear,
                    SignalType = "4-20mA", ModuleType = "AI-871",
                    RawLow = 4, RawHigh = 20, EuLow = 0, EuHigh = 250, EuLowSi = 0, EuHighSi = 250,
                },
            };

            var tolerances = new ToleranceLibrary();
            var def = new ToleranceDefinition { SignalType = "4-20mA", ModuleType = "AI-871" };
            def.Terms.Add(new ToleranceTerm { Kind = ToleranceTermKind.Percent, Value = 0.003, PercentBasis = PercentBasis.RawSpan });
            tolerances.Add(def);

            var runner = new DatasheetRunner(new SignalResolver(signals, AliasTableSet.Empty()), tolerances, ScaleCurveLibrary.CreateDefault());
            return (sheet, mapping, runner);
        }

        [Fact]
        public void Apply_WritesTheCalculatedToleranceAndClearsOldComments()
        {
            (FakeDatasheet sheet, DatasheetMapping mapping, DatasheetRunner runner) = Setup(new[]
            {
                new string?[] { "Signal", "Expected", "Tol" },
                new string?[] { "FT-201 Flow", "125", "" },
            });
            sheet.Comments.Add((1, 2, "[ToleranceTool] stale"));

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Apply);

            Assert.True(result.DidRun);
            Assert.Equal(1, result.Written);
            Assert.Equal(0.75, sheet.Written[(1, 2)], 6);
            Assert.Empty(sheet.Comments);
        }

        [Fact]
        public void Check_CommentsWhereTheExistingValueDisagrees_AndWritesNothing()
        {
            (FakeDatasheet sheet, DatasheetMapping mapping, DatasheetRunner runner) = Setup(new[]
            {
                new string?[] { "Signal", "Expected", "Tol" },
                new string?[] { "FT-201", "125", "0.9" },
                new string?[] { "FT-201", "125", "0.75" },
            });

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Check);

            Assert.Empty(sheet.Written);
            Assert.Equal(1, result.Mismatched);
            Assert.Equal(1, result.Matches);
            Assert.Single(sheet.Comments);
            Assert.Equal(1, sheet.Comments[0].Row);
        }

        [Fact]
        public void Run_ReportsUnresolvedRowsAndSkipsThem()
        {
            (FakeDatasheet sheet, DatasheetMapping mapping, DatasheetRunner runner) = Setup(new[]
            {
                new string?[] { "Signal", "Expected", "Tol" },
                new string?[] { "Mystery Signal", "10", "" },
            });

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Apply);

            Assert.Equal(RowStatus.NoSignal, result.Rows.Single().Status);
            Assert.Empty(sheet.Written);
        }

        [Fact]
        public void Run_ReportsMissingToleranceDefinitions()
        {
            (FakeDatasheet sheet, DatasheetMapping mapping, DatasheetRunner runner) = Setup(new[]
            {
                new string?[] { "Signal", "Expected", "Tol" },
                new string?[] { "FT-201", "abc", "" },
            });

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Apply);
            Assert.Equal(RowStatus.NotCalculable, result.Rows.Single().Status);
        }

        [Fact]
        public void Apply_MatchExpected_RoundsToTheShownSignificantDigits()
        {
            (FakeDatasheet sheet, DatasheetMapping mapping, DatasheetRunner runner) =
                Setup(new[]
                {
                    new string?[] { "Signal", "Expected", "Tol" },
                    new string?[] { "FT-201", "125", "" },
                }, PrecisionPolicy.MatchExpected());

            sheet.Display[(1, 1)] = "125.0"; // 4 significant digits shown

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Apply);

            // exact tolerance is 0.75; 4 sig figs -> 0.7500
            Assert.Equal(0.75, sheet.Written[(1, 2)], 6);
            Assert.Equal(1, result.Written);
        }

        [Fact]
        public void Run_FailsCleanlyWhenRequiredHeadersAreNotMapped()
        {
            var sheet = new FakeDatasheet("T", new[] { new string?[] { "A", "B" } });
            var mapping = new DatasheetMapping { HeaderRowIndex = 0 };
            var runner = new DatasheetRunner(
                new SignalResolver(new List<SignalConfig>()), new ToleranceLibrary(), ScaleCurveLibrary.CreateDefault());

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Check);
            Assert.False(result.DidRun);
            Assert.NotEmpty(result.SetupProblems);
        }

        [Fact]
        public void RunPassFail_WritesPassOrFailFromTheSheetValues()
        {
            var sheet = new FakeDatasheet("PF", new[]
            {
                new string?[] { "Signal", "Expected", "Tol", "Actual", "Pass/Fail" },
                new string?[] { "A", "100", "0.5", "100.3", "" },
                new string?[] { "B", "100", "0.5", "101.0", "" },
                new string?[] { "C", "100", "", "100.0", "" },
            });

            var mapping = new DatasheetMapping { HeaderRowIndex = 0 };
            mapping.Headers[DatasheetParameter.SystemId] = "Signal";
            mapping.Headers[DatasheetParameter.Expected] = "Expected";
            mapping.Headers[DatasheetParameter.Tolerance] = "Tol";
            mapping.Headers[DatasheetParameter.Actual] = "Actual";
            mapping.Headers[DatasheetParameter.PassFail] = "Pass/Fail";

            var runner = new DatasheetRunner(
                new SignalResolver(new List<SignalConfig>()), new ToleranceLibrary(), ScaleCurveLibrary.CreateDefault());

            DatasheetRunResult result = runner.RunPassFail(sheet, mapping);

            Assert.Equal("Pass", sheet.WrittenText[(1, 4)]);
            Assert.Equal("Fail", sheet.WrittenText[(2, 4)]);
            Assert.False(sheet.WrittenText.ContainsKey((3, 4))); // missing tolerance -> not evaluated
        }

        [Fact]
        public void Run_ReportsAnUnresolvedMappedHeader()
        {
            (FakeDatasheet sheet, DatasheetMapping mapping, DatasheetRunner runner) = Setup(new[]
            {
                new string?[] { "Signal", "Expected", "Tol" },
                new string?[] { "FT-201", "125", "" },
            });
            mapping.Headers[DatasheetParameter.Actual] = "DoesNotExist";

            DatasheetRunResult result = runner.Run(sheet, mapping, DatasheetRunMode.Apply);
            Assert.False(result.DidRun);
        }
    }

    public class SignificantDigitsTests
    {
        [Theory]
        [InlineData("125", 3)]
        [InlineData("125.0", 4)]
        [InlineData("0.0480", 3)]
        [InlineData("1200", 4)]
        [InlineData("-3.14", 3)]
        [InlineData("1.5e3", 2)]
        [InlineData("", null)]
        [InlineData("n/a", null)]
        [InlineData("#DIV/0!", null)]
        public void Count_MatchesWhatIsShown(string text, int? expected)
        {
            Assert.Equal(expected, SignificantDigits.Count(text));
        }
    }

    public class DatasheetMappingXmlTests
    {
        [Fact]
        public void RoundTrips()
        {
            var mapping = new DatasheetMapping
            {
                HeaderRowIndex = 2,
                FirstDataRowIndex = 3,
                LastDataRowIndex = 40,
                DefaultUnitSystem = Core.Signals.UnitSystem.Si,
                UnitColumnHeader = "Units",
                Precision = PrecisionPolicy.SignificantFigures(4, RoundingMode.HalfUp),
            };
            mapping.Headers[DatasheetParameter.SystemId] = "Signal Name";
            mapping.Headers[DatasheetParameter.Expected] = "Expect'd";
            mapping.Headers[DatasheetParameter.Tolerance] = "Tol";
            mapping.ResolutionOverrides["Loop 4"] = "UT-1002";

            string xml = DatasheetMappingXml.ToXml(mapping);
            DatasheetMapping reloaded = DatasheetMappingXml.FromXml(xml).Value;

            Assert.Equal(2, reloaded.HeaderRowIndex);
            Assert.Equal(3, reloaded.FirstDataRowIndex);
            Assert.Equal(40, reloaded.LastDataRowIndex);
            Assert.Equal(Core.Signals.UnitSystem.Si, reloaded.DefaultUnitSystem);
            Assert.Equal("Units", reloaded.UnitColumnHeader);
            Assert.Equal(PrecisionMode.SignificantFigures, reloaded.Precision.Mode);
            Assert.Equal(4, reloaded.Precision.Digits);
            Assert.Equal(RoundingMode.HalfUp, reloaded.Precision.Rounding);
            Assert.Equal("Expect'd", reloaded.Header(DatasheetParameter.Expected));
            Assert.Equal("UT-1002", reloaded.ResolutionOverrides["Loop 4"]);
        }
    }
}
