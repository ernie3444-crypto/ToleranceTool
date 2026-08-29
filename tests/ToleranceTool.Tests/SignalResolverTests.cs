using System.Collections.Generic;
using System.IO;
using ToleranceTool.Configuration.Aliases;
using ToleranceTool.Configuration.Datasheet;
using ToleranceTool.Core.Signals;
using Xunit;

namespace ToleranceTool.Tests
{
    public class SignalResolverTests
    {
        private static List<SignalConfig> Signals() => new List<SignalConfig>
        {
            new SignalConfig { UniversalId = "UT-1001", SensorName = "FT-201", SignalType = "4-20mA", ModuleType = "AI-871" },
            new SignalConfig { UniversalId = "UT-1002", SensorName = "PT-330", SignalType = "4-20mA", ModuleType = "AI-871" },
            new SignalConfig { UniversalId = "UT-1003", SensorName = "TT-115", SignalType = "0-10V", ModuleType = "AI-664" },
        };

        [Fact]
        public void Exact_MatchesASensorNameDirectly()
        {
            var resolver = new SignalResolver(Signals());
            SignalResolution result = resolver.Resolve("  ft-201 ");

            Assert.Equal(ResolutionStep.Exact, result.Step);
            Assert.Equal("UT-1001", result.Signal!.UniversalId);
        }

        [Fact]
        public void AutoMatch_WhenExactlyOneSensorNameIsAWholeToken()
        {
            var resolver = new SignalResolver(Signals());
            SignalResolution result = resolver.Resolve("FT-201 Flow Loop");

            Assert.Equal(ResolutionStep.AutoMatch, result.Step);
            Assert.Equal("UT-1001", result.Signal!.UniversalId);
        }

        [Fact]
        public void AutoMatch_DoesNotFireOnAPartialTokenMatch()
        {
            var signals = new List<SignalConfig>
            {
                new SignalConfig { UniversalId = "U1", SensorName = "T-11" },
            };
            var resolver = new SignalResolver(signals);

            Assert.Equal(ResolutionStep.Unresolved, resolver.Resolve("XT-115 Temperature").Step);
        }

        [Fact]
        public void Ambiguous_WhenTwoSensorNamesMatch()
        {
            var signals = new List<SignalConfig>
            {
                new SignalConfig { UniversalId = "U1", SensorName = "FT-201" },
                new SignalConfig { UniversalId = "U2", SensorName = "201" },
            };
            var resolver = new SignalResolver(signals);
            SignalResolution result = resolver.Resolve("FT-201 Flow");

            Assert.Equal(ResolutionStep.Ambiguous, result.Step);
            Assert.False(result.IsResolved);
            Assert.Equal(2, result.Candidates.Count);
        }

        [Fact]
        public void Alias_ExactAndRegexEntriesResolve()
        {
            var set = new AliasTableSet();
            var table = new AliasTable { Name = "Project X", Priority = 10 };
            table.Entries.Add(new AliasEntry { SystemId = "Loop 4 Pressure", UniversalId = "UT-1002", Match = AliasMatch.Exact });
            table.Entries.Add(new AliasEntry { SystemId = @"^(TT-\d+).*$", SensorName = "$1", Match = AliasMatch.Regex });
            set.Add(table);

            var resolver = new SignalResolver(Signals(), set);

            Assert.Equal("UT-1002", resolver.Resolve("Loop 4 Pressure").Signal!.UniversalId);
            Assert.Equal("UT-1003", resolver.Resolve("TT-115 sensor").Signal!.UniversalId);
        }

        [Fact]
        public void Override_SitsAboveTheLadder()
        {
            var overrides = new Dictionary<string, string> { ["FT-201"] = "UT-1003" };
            var resolver = new SignalResolver(Signals(), null, overrides);

            SignalResolution result = resolver.Resolve("FT-201");
            Assert.Equal(ResolutionStep.Override, result.Step);
            Assert.Equal("UT-1003", result.Signal!.UniversalId);
        }

        [Fact]
        public void AliasTables_AreConsultedInPriorityOrder()
        {
            var set = new AliasTableSet();
            var low = new AliasTable { Name = "low priority", Priority = 100 };
            low.Entries.Add(new AliasEntry { SystemId = "X", UniversalId = "UT-1002", Match = AliasMatch.Exact });
            var high = new AliasTable { Name = "high priority", Priority = 1 };
            high.Entries.Add(new AliasEntry { SystemId = "X", UniversalId = "UT-1001", Match = AliasMatch.Exact });
            set.Add(low);
            set.Add(high);

            var resolver = new SignalResolver(Signals(), set);
            Assert.Equal("UT-1001", resolver.Resolve("X").Signal!.UniversalId);
        }
    }

    public class AliasTablesXmlTests
    {
        private const string Sample = @"<AliasTables>
  <AliasTable name='Project X' priority='10'>
    <Alias systemId='FT-201-A' sensorName='FT-201' match='exact' />
    <Alias systemId='^(FT-\d+).*$' sensorName='$1' match='regex' />
    <Alias systemId='Loop 4 Pressure' universalId='UT-1002' match='exact' />
  </AliasTable>
</AliasTables>";

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            AliasTableSet original = AliasTablesXml.Load(new StringReader(Sample)).Value;
            Assert.Single(original.Tables);
            Assert.Equal(3, original.Tables[0].Entries.Count);

            var buffer = new StringWriter();
            AliasTablesXml.Save(original, buffer);
            AliasTableSet reloaded = AliasTablesXml.Load(new StringReader(buffer.ToString())).Value;

            Assert.Equal(3, reloaded.Tables[0].Entries.Count);
            Assert.Equal(AliasMatch.Regex, reloaded.Tables[0].Entries[1].Match);
            Assert.Equal("UT-1002", reloaded.Tables[0].Entries[2].UniversalId);
        }

        [Fact]
        public void Load_RejectsAnEntryWithBothOrNeitherTarget()
        {
            var result = AliasTablesXml.Load(new StringReader(
                "<AliasTables><AliasTable name='t'><Alias systemId='a' sensorName='b' universalId='c' match='exact'/></AliasTable></AliasTables>"));

            Assert.True(result.HasErrors);
        }
    }
}
