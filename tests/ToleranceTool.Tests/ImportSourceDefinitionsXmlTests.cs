using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToleranceTool.Import;
using Xunit;

namespace ToleranceTool.Tests
{
    public class ImportSourceDefinitionsXmlTests
    {
        [Fact]
        public void SaveThenLoad_RoundTripsSourcesAndFieldMappings()
        {
            var master = new ImportSourceDefinition("master.csv", SignalSourceKind.DelimitedText, @"C:\data\master.csv")
            {
                IsMaster = true,
                UniversalIdLocator = "A",
                HeaderRowIndex = 0,
            };
            master.Fields.Add(new FieldBinding(SignalField.SensorName, "B", true));
            master.Fields.Add(new FieldBinding(SignalField.SignalType, "K", true));
            master.Fields.Add(new FieldBinding(SignalField.RawLow, "", false));

            var db = new ImportSourceDefinition("plant", SignalSourceKind.Access, "Provider=x;Data Source=y;")
            {
                Query = "SELECT * FROM Signals",
                UniversalIdLocator = "UID",
            };
            db.Fields.Add(new FieldBinding(SignalField.EuLow, "Lo", true));

            string path = Path.Combine(Path.GetTempPath(), $"tt-sources-{System.Guid.NewGuid():N}.xml");
            try
            {
                ImportSourceDefinitionsXml.Save(new[] { master, db }, path);
                List<ImportSourceDefinition> loaded = ImportSourceDefinitionsXml.Load(path);

                Assert.Equal(2, loaded.Count);

                ImportSourceDefinition m = loaded[0];
                Assert.True(m.IsMaster);
                Assert.Equal(SignalSourceKind.DelimitedText, m.Kind);
                Assert.Equal("A", m.UniversalIdLocator);
                Assert.Equal(0, m.HeaderRowIndex);
                Assert.Equal("B", m.Binding(SignalField.SensorName)!.Locator);
                Assert.True(m.Binding(SignalField.SignalType)!.Required);
                Assert.False(m.Binding(SignalField.RawLow)!.Required);

                ImportSourceDefinition d = loaded[1];
                Assert.Equal(SignalSourceKind.Access, d.Kind);
                Assert.Equal("SELECT * FROM Signals", d.Query);
                Assert.Equal("Lo", d.Binding(SignalField.EuLow)!.Locator);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Load_ReturnsEmptyWhenTheFileIsMissing()
        {
            Assert.Empty(ImportSourceDefinitionsXml.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-xyz.xml")));
        }
    }
}
