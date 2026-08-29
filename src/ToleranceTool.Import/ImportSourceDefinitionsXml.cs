using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace ToleranceTool.Import
{
    /// <summary>
    /// Persists the import wizard's source definitions and field mappings so the
    /// setup survives closing the dialog. Stopgap for the in-workbook store.
    /// </summary>
    public static class ImportSourceDefinitionsXml
    {
        private const string Root = "ImportSources";

        public static void Save(IEnumerable<ImportSourceDefinition> sources, string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var root = new XElement(Root);
            foreach (ImportSourceDefinition source in sources)
            {
                var element = new XElement(
                    "Source",
                    new XAttribute("name", source.Name),
                    new XAttribute("kind", source.Kind),
                    new XAttribute("location", source.Location),
                    new XAttribute("master", source.IsMaster),
                    new XAttribute("orientation", source.Orientation),
                    new XAttribute("universalIdLocator", source.UniversalIdLocator));

                if (source.HeaderRowIndex.HasValue)
                {
                    element.Add(new XAttribute("headerRow", source.HeaderRowIndex.Value));
                }

                if (!string.IsNullOrEmpty(source.SheetName))
                {
                    element.Add(new XAttribute("sheet", source.SheetName));
                }

                if (!string.IsNullOrEmpty(source.Query))
                {
                    element.Add(new XAttribute("query", source.Query));
                }

                foreach (FieldBinding binding in source.Fields)
                {
                    element.Add(new XElement(
                        "Field",
                        new XAttribute("name", binding.Field),
                        new XAttribute("locator", binding.Locator),
                        new XAttribute("required", binding.Required)));
                }

                root.Add(element);
            }

            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(path);
        }

        public static List<ImportSourceDefinition> Load(string path)
        {
            var sources = new List<ImportSourceDefinition>();
            if (!File.Exists(path))
            {
                return sources;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (XmlException)
            {
                return sources;
            }

            foreach (XElement element in document.Root?.Elements("Source") ?? System.Linq.Enumerable.Empty<XElement>())
            {
                if (!Enum.TryParse((string?)element.Attribute("kind"), out SignalSourceKind kind))
                {
                    kind = SignalSourceKind.DelimitedText;
                }

                var source = new ImportSourceDefinition(
                    (string?)element.Attribute("name") ?? "source",
                    kind,
                    (string?)element.Attribute("location") ?? string.Empty)
                {
                    IsMaster = (bool?)element.Attribute("master") ?? false,
                    UniversalIdLocator = (string?)element.Attribute("universalIdLocator") ?? "A",
                    SheetName = (string?)element.Attribute("sheet"),
                    Query = (string?)element.Attribute("query"),
                };

                if (Enum.TryParse((string?)element.Attribute("orientation"), out SignalDataOrientation orientation))
                {
                    source.Orientation = orientation;
                }

                source.HeaderRowIndex = int.TryParse((string?)element.Attribute("headerRow"), out int header) ? header : (int?)null;

                foreach (XElement field in element.Elements("Field"))
                {
                    string name = (string?)field.Attribute("name") ?? string.Empty;
                    if (name.Length == 0)
                    {
                        continue;
                    }

                    source.Fields.Add(new FieldBinding(
                        name,
                        (string?)field.Attribute("locator") ?? string.Empty,
                        (bool?)field.Attribute("required") ?? false));
                }

                sources.Add(source);
            }

            return sources;
        }
    }
}
