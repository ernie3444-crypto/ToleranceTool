using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Configuration.SignalTypes
{
    /// <summary>Reads and writes <c>signal-types.xml</c>.</summary>
    public static class SignalTypeRegistryXml
    {
        private const string Root = "SignalTypes";
        private const string Item = "SignalType";

        public static ConfigLoadResult<SignalTypeRegistry> Load(string path)
        {
            if (!File.Exists(path))
            {
                return new ConfigLoadResult<SignalTypeRegistry>(
                    new SignalTypeRegistry(),
                    new[] { ConfigIssue.Error($"The signal-type registry file was not found: {path}") });
            }

            using (var reader = new StreamReader(path))
            {
                return Load(reader);
            }
        }

        public static ConfigLoadResult<SignalTypeRegistry> Load(TextReader reader)
        {
            var issues = new List<ConfigIssue>();
            var registry = new SignalTypeRegistry();

            XDocument document;
            try
            {
                document = XDocument.Load(reader);
            }
            catch (XmlException ex)
            {
                issues.Add(ConfigIssue.Error($"The signal-type registry is not well-formed XML: {ex.Message}"));
                return new ConfigLoadResult<SignalTypeRegistry>(registry, issues);
            }

            XElement? root = document.Root;
            if (root == null || root.Name.LocalName != Root)
            {
                issues.Add(ConfigIssue.Error($"The root element must be <{Root}>."));
                return new ConfigLoadResult<SignalTypeRegistry>(registry, issues);
            }

            foreach (XElement element in root.Elements(Item))
            {
                string name = ((string?)element.Attribute("name") ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    issues.Add(ConfigIssue.Error($"A <{Item}> has no name."));
                    continue;
                }

                if (!TryDouble(element, "rawLow", name, issues, out double low) ||
                    !TryDouble(element, "rawHigh", name, issues, out double high))
                {
                    continue;
                }

                if (low == high)
                {
                    issues.Add(ConfigIssue.Warning("rawLow equals rawHigh; the raw span is zero.", name));
                }

                registry.AddRaw(new SignalTypeSpec
                {
                    Name = name,
                    RawLow = low,
                    RawHigh = high,
                    Unit = ((string?)element.Attribute("unit") ?? string.Empty).Trim(),
                });
            }

            foreach (string duplicate in registry.DuplicateNames())
            {
                issues.Add(ConfigIssue.Error("This signal type is defined more than once.", duplicate));
            }

            return new ConfigLoadResult<SignalTypeRegistry>(registry, issues);
        }

        public static void Save(SignalTypeRegistry registry, string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(path))
            {
                Save(registry, writer);
            }
        }

        public static void Save(SignalTypeRegistry registry, TextWriter writer)
        {
            var root = new XElement(Root);
            foreach (SignalTypeSpec spec in registry.Specs)
            {
                var element = new XElement(
                    Item,
                    new XAttribute("name", spec.Name),
                    new XAttribute("rawLow", spec.RawLow.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("rawHigh", spec.RawHigh.ToString("R", CultureInfo.InvariantCulture)));

                if (!string.IsNullOrEmpty(spec.Unit))
                {
                    element.Add(new XAttribute("unit", spec.Unit));
                }

                root.Add(element);
            }

            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(writer, SaveOptions.None);
        }

        private static bool TryDouble(XElement element, string attribute, string scope, List<ConfigIssue> issues, out double value)
        {
            value = 0;
            string? text = (string?)element.Attribute(attribute);
            if (text == null)
            {
                issues.Add(ConfigIssue.Error($"<{Item}> is missing \"{attribute}\".", scope));
                return false;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                issues.Add(ConfigIssue.Error($"<{Item}> has a non-numeric \"{attribute}\": \"{text}\".", scope));
                return false;
            }

            return true;
        }
    }
}
