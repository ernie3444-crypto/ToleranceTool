using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using ToleranceTool.Core.Scales;

namespace ToleranceTool.Configuration.Scales
{
    /// <summary>
    /// Reads and writes <c>scale-types.xml</c>. Each entry is validated numerically
    /// against the library contract (Forward(0)=0, Forward(1)=1, monotonic) via
    /// <see cref="ScaleCurve"/>.
    /// </summary>
    public static class ScaleTypeLibraryXml
    {
        private const string Root = "ScaleTypes";
        private const string Item = "ScaleType";

        public static ConfigLoadResult<List<ScaleType>> Load(string path)
        {
            if (!File.Exists(path))
            {
                return new ConfigLoadResult<List<ScaleType>>(
                    new List<ScaleType>(),
                    new[] { ConfigIssue.Error($"The scale-type library file was not found: {path}") });
            }

            using (var reader = new StreamReader(path))
            {
                return Load(reader);
            }
        }

        public static ConfigLoadResult<List<ScaleType>> Load(TextReader reader)
        {
            var issues = new List<ConfigIssue>();
            var scaleTypes = new List<ScaleType>();

            XDocument document;
            try
            {
                document = XDocument.Load(reader);
            }
            catch (XmlException ex)
            {
                issues.Add(ConfigIssue.Error($"The scale-type library is not well-formed XML: {ex.Message}"));
                return new ConfigLoadResult<List<ScaleType>>(scaleTypes, issues);
            }

            XElement? root = document.Root;
            if (root == null || root.Name.LocalName != Root)
            {
                issues.Add(ConfigIssue.Error($"The root element must be <{Root}>."));
                return new ConfigLoadResult<List<ScaleType>>(scaleTypes, issues);
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement element in root.Elements(Item))
            {
                string name = ((string?)element.Attribute("name") ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    issues.Add(ConfigIssue.Error($"A <{Item}> has no name."));
                    continue;
                }

                if (!names.Add(name))
                {
                    issues.Add(ConfigIssue.Error("This scale type is defined more than once.", name));
                    continue;
                }

                string forward = (element.Element("Forward")?.Value ?? string.Empty).Trim();
                string inverse = (element.Element("Inverse")?.Value ?? string.Empty).Trim();
                if (forward.Length == 0 || inverse.Length == 0)
                {
                    issues.Add(ConfigIssue.Error("A scale type needs both a <Forward> and an <Inverse> expression.", name));
                    continue;
                }

                var scaleType = new ScaleType { Name = name, Forward = forward, Inverse = inverse };

                bool parametersOk = true;
                foreach (XElement param in element.Elements("Param"))
                {
                    string paramName = ((string?)param.Attribute("name") ?? string.Empty).Trim();
                    string paramText = (string?)param.Attribute("value") ?? string.Empty;
                    if (paramName.Length == 0 ||
                        !double.TryParse(paramText, NumberStyles.Float, CultureInfo.InvariantCulture, out double paramValue))
                    {
                        issues.Add(ConfigIssue.Error($"A <Param> is missing a name or has a non-numeric value.", name));
                        parametersOk = false;
                        break;
                    }

                    scaleType.Parameters[paramName] = paramValue;
                }

                if (!parametersOk)
                {
                    continue;
                }

                ValidateCurve(scaleType, issues);
                scaleTypes.Add(scaleType);
            }

            return new ConfigLoadResult<List<ScaleType>>(scaleTypes, issues);
        }

        /// <summary>Parses + numerically checks one scale type. Returns the problems (empty when valid).</summary>
        public static IReadOnlyList<ConfigIssue> ValidateCurve(ScaleType scaleType)
        {
            var issues = new List<ConfigIssue>();
            ValidateCurve(scaleType, issues);
            return issues;
        }

        private static void ValidateCurve(ScaleType scaleType, List<ConfigIssue> issues)
        {
            ScaleCurve curve;
            try
            {
                curve = new ScaleCurve(scaleType);
            }
            catch (Exception ex)
            {
                issues.Add(ConfigIssue.Error(ex.Message, scaleType.Name));
                return;
            }

            foreach (string problem in curve.Validate())
            {
                issues.Add(ConfigIssue.Error(problem, scaleType.Name));
            }
        }

        public static void Save(IEnumerable<ScaleType> scaleTypes, string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(path))
            {
                Save(scaleTypes, writer);
            }
        }

        public static void Save(IEnumerable<ScaleType> scaleTypes, TextWriter writer)
        {
            var root = new XElement(Root);
            foreach (ScaleType scaleType in scaleTypes)
            {
                var element = new XElement(Item, new XAttribute("name", scaleType.Name));
                foreach (KeyValuePair<string, double> parameter in scaleType.Parameters)
                {
                    element.Add(new XElement(
                        "Param",
                        new XAttribute("name", parameter.Key),
                        new XAttribute("value", parameter.Value.ToString("R", CultureInfo.InvariantCulture))));
                }

                element.Add(new XElement("Forward", scaleType.Forward));
                element.Add(new XElement("Inverse", scaleType.Inverse));
                root.Add(element);
            }

            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(writer, SaveOptions.None);
        }
    }
}
