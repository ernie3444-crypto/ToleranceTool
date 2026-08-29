using System.Collections.Generic;
using System.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Import;
using ToleranceTool.Import.Access;
using Xunit;

namespace ToleranceTool.Tests
{
    public class AccessConnectionTests
    {
        [Fact]
        public void ForDatabase_PicksAceForAccdb_AndJetForMdb()
        {
            Assert.Contains(AccessConnection.AceProvider, AccessConnection.ForDatabase(@"C:\data\signals.accdb"));
            Assert.Contains(AccessConnection.JetProvider, AccessConnection.ForDatabase(@"C:\data\legacy.mdb"));
        }

        [Fact]
        public void ForDatabase_PassesThroughAFullConnectionString()
        {
            const string cs = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=x.accdb;";
            Assert.Equal(cs, AccessConnection.ForDatabase(cs));
            Assert.True(AccessConnection.LooksLikeConnectionString(cs));
        }
    }

    public class AccessSignalSourceTests
    {
        private sealed class FakeReader : IRecordReader
        {
            private readonly List<Dictionary<string, object?>> _rows;
            private int _index = -1;

            public FakeReader(IEnumerable<string> columns, List<Dictionary<string, object?>> rows)
            {
                Columns = columns.ToList();
                _rows = rows;
            }

            public IReadOnlyList<string> Columns { get; }

            public bool Read() => ++_index < _rows.Count;

            public object? Value(string column) =>
                _rows[_index].TryGetValue(column, out object? value) ? value : null;
        }

        private static ImportSourceDefinition Definition()
        {
            var def = new ImportSourceDefinition("db", SignalSourceKind.Access, "Provider=x;Data Source=y;")
            {
                Query = "SELECT * FROM Signals",
                UniversalIdLocator = "UID",
                IsMaster = true,
            };
            def.Fields.Add(new FieldBinding(SignalField.SensorName, "Sensor", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLow, "Lo", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHigh, "Hi", true));
            return def;
        }

        [Fact]
        public void Map_TurnsResultRowsIntoFieldRecords()
        {
            ImportSourceDefinition def = Definition();
            var reader = new FakeReader(
                new[] { "UID", "Sensor", "Lo", "Hi" },
                new List<Dictionary<string, object?>>
                {
                    new Dictionary<string, object?> { ["UID"] = "UT-1", ["Sensor"] = "FT-201", ["Lo"] = 0.0, ["Hi"] = 250.0 },
                    new Dictionary<string, object?> { ["UID"] = " ", ["Sensor"] = "skip", ["Lo"] = 0.0, ["Hi"] = 1.0 },
                    new Dictionary<string, object?> { ["UID"] = "UT-2", ["Sensor"] = "PT-330", ["Lo"] = 0.0, ["Hi"] = 100.0 },
                });

            IReadOnlyList<SignalFieldRecord> records = AccessSignalSource.Map(reader, def);

            Assert.Equal(2, records.Count);
            Assert.Equal("UT-1", records[0].UniversalId);
            Assert.Equal("FT-201", records[0].Fields[SignalField.SensorName]);
            Assert.Equal("250", records[0].Fields[SignalField.EuHigh]);
        }

        [Fact]
        public void AccessSource_PluggedIntoTheJoinLikeAnyOtherSource()
        {
            ImportSourceDefinition def = Definition();
            def.Fields.Add(new FieldBinding(SignalField.ConversionSense, "Sense", true));
            def.Fields.Add(new FieldBinding(SignalField.ScaleType, "Scale", true));
            def.Fields.Add(new FieldBinding(SignalField.SignalType, "SigType", true));
            def.Fields.Add(new FieldBinding(SignalField.ModuleType, "Module", true));
            def.Fields.Add(new FieldBinding(SignalField.EuLowSi, "LoSI", true));
            def.Fields.Add(new FieldBinding(SignalField.EuHighSi, "HiSI", true));

            var reader = new FakeReader(
                new[] { "UID", "Sensor", "Lo", "Hi", "Sense", "Scale", "SigType", "Module", "LoSI", "HiSI" },
                new List<Dictionary<string, object?>>
                {
                    new Dictionary<string, object?>
                    {
                        ["UID"] = "UT-1", ["Sensor"] = "FT-201", ["Lo"] = 0.0, ["Hi"] = 250.0,
                        ["Sense"] = "Direct", ["Scale"] = "Linear", ["SigType"] = "4-20mA", ["Module"] = "AI-871",
                        ["LoSI"] = 0.0, ["HiSI"] = 121.1,
                    },
                });

            var source = new AccessSignalSource(def, () => reader);
            ConfigLoadResult<ResolvedSignalSet> result = new SignalSetBuilder().Add(source, def).Build();

            Assert.False(result.HasErrors);
            ResolvedSignal signal = result.Value.Find("UT-1")!;
            Assert.True(signal.IsComplete);
            Assert.Equal("4-20mA", signal.Config.SignalType);
            Assert.Equal(121.1, signal.Config.EuHighSi, 6);
        }
    }
}
