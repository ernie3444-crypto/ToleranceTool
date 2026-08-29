using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace ToleranceTool.Configuration.Aliases
{
    /// <summary>Reads and writes <c>alias-tables.xml</c> (and the equivalent workbook part).</summary>
    public static class AliasTablesXml
    {
        private const string Root = "AliasTables";

        public static ConfigLoadResult<AliasTableSet> Load(string path)
        {
            if (!File.Exists(path))
            {
                return new ConfigLoadResult<AliasTableSet>(
                    new AliasTableSet(),
                    new[] { ConfigIssue.Error($"The alias-tables file was not found: {path}") });
            }

            using (var reader = new StreamReader(path))
            {
                return Load(reader);
            }
        }

        public static ConfigLoadResult<AliasTableSet> Load(TextReader reader)
        {
            var issues = new List<ConfigIssue>();
            var set = new AliasTableSet();

            XDocument document;
            try
            {
                document = XDocument.Load(reader);
            }
            catch (XmlException ex)
            {
                issues.Add(ConfigIssue.Error($"The alias tables are not well-formed XML: {ex.Message}"));
                return new ConfigLoadResult<AliasTableSet>(set, issues);
            }

            XElement? root = document.Root;
            if (root == null || root.Name.LocalName != Root)
            {
                issues.Add(ConfigIssue.Error($"The root element must be <{Root}>."));
                return new ConfigLoadResult<AliasTableSet>(set, issues);
            }

            foreach (XElement tableElement in root.Elements("AliasTable"))
            {
                var table = new AliasTable
                {
                    Name = ((string?)tableElement.Attribute("name") ?? string.Empty).Trim(),
                    Priority = int.TryParse((string?)tableElement.Attribute("priority"), out int priority) ? priority : 0,
                };

                foreach (XElement aliasElement in tableElement.Elements("Alias"))
                {
                    string systemId = ((string?)aliasElement.Attribute("systemId") ?? string.Empty).Trim();
                    if (systemId.Length == 0)
                    {
                        issues.Add(ConfigIssue.Error("An <Alias> has no systemId.", table.Name));
                        continue;
                    }

                    string? sensorName = Nullable((string?)aliasElement.Attribute("sensorName"));
                    string? universalId = Nullable((string?)aliasElement.Attribute("universalId"));
                    if ((sensorName == null) == (universalId == null))
                    {
                        issues.Add(ConfigIssue.Error(
                            $"Alias \"{systemId}\" must set exactly one of sensorName / universalId.", table.Name));
                        continue;
                    }

                    if (!TryMatch((string?)aliasElement.Attribute("match"), out AliasMatch match))
                    {
                        issues.Add(ConfigIssue.Error(
                            $"Alias \"{systemId}\" has an unknown match mode. Use exact, contains, or regex.", table.Name));
                        continue;
                    }

                    table.Entries.Add(new AliasEntry
                    {
                        SystemId = systemId,
                        SensorName = sensorName,
                        UniversalId = universalId,
                        Match = match,
                    });
                }

                set.Add(table);
            }

            return new ConfigLoadResult<AliasTableSet>(set, issues);
        }

        public static void Save(AliasTableSet set, string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(path))
            {
                Save(set, writer);
            }
        }

        public static void Save(AliasTableSet set, TextWriter writer)
        {
            var root = new XElement(Root);
            foreach (AliasTable table in set.Tables)
            {
                var tableElement = new XElement(
                    "AliasTable",
                    new XAttribute("name", table.Name),
                    new XAttribute("priority", table.Priority.ToString(CultureInfo.InvariantCulture)));

                foreach (AliasEntry entry in table.Entries)
                {
                    var aliasElement = new XElement("Alias", new XAttribute("systemId", entry.SystemId));
                    if (entry.SensorName != null)
                    {
                        aliasElement.Add(new XAttribute("sensorName", entry.SensorName));
                    }

                    if (entry.UniversalId != null)
                    {
                        aliasElement.Add(new XAttribute("universalId", entry.UniversalId));
                    }

                    aliasElement.Add(new XAttribute("match", entry.Match.ToString().ToLowerInvariant()));
                    tableElement.Add(aliasElement);
                }

                root.Add(tableElement);
            }

            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(writer, SaveOptions.None);
        }

        private static string? Nullable(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

        private static bool TryMatch(string? text, out AliasMatch match)
        {
            switch ((text ?? "exact").Trim().ToLowerInvariant())
            {
                case "exact":
                    match = AliasMatch.Exact;
                    return true;
                case "contains":
                    match = AliasMatch.Contains;
                    return true;
                case "regex":
                    match = AliasMatch.Regex;
                    return true;
                default:
                    match = AliasMatch.Exact;
                    return false;
            }
        }
    }
}
