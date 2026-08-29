using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using ToleranceTool.Core.Precision;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Configuration.Datasheet
{
    /// <summary>Serializes a <see cref="DatasheetMapping"/> to and from the XML stored in the workbook.</summary>
    public static class DatasheetMappingXml
    {
        public static string ToXml(DatasheetMapping mapping)
        {
            var root = new XElement(
                "DatasheetMapping",
                new XAttribute("orientation", mapping.Orientation),
                new XAttribute("headerRow", mapping.HeaderRowIndex),
                new XAttribute("defaultUnitSystem", mapping.DefaultUnitSystem));

            if (mapping.FirstDataRowIndex.HasValue)
            {
                root.Add(new XAttribute("firstDataRow", mapping.FirstDataRowIndex.Value));
            }

            if (mapping.LastDataRowIndex.HasValue)
            {
                root.Add(new XAttribute("lastDataRow", mapping.LastDataRowIndex.Value));
            }

            if (!string.IsNullOrWhiteSpace(mapping.UnitColumnHeader))
            {
                root.Add(new XAttribute("unitColumnHeader", mapping.UnitColumnHeader));
            }

            var headers = new XElement("Headers");
            foreach (var pair in mapping.Headers)
            {
                headers.Add(new XElement("Header", new XAttribute("parameter", pair.Key), new XAttribute("text", pair.Value)));
            }

            root.Add(headers);

            root.Add(new XElement(
                "Precision",
                new XAttribute("mode", mapping.Precision.Mode),
                new XAttribute("digits", mapping.Precision.Digits),
                new XAttribute("rounding", mapping.Precision.Rounding)));

            if (mapping.ResolutionOverrides.Count > 0)
            {
                var overrides = new XElement("ResolutionOverrides");
                foreach (var pair in mapping.ResolutionOverrides)
                {
                    overrides.Add(new XElement("Override", new XAttribute("systemId", pair.Key), new XAttribute("universalId", pair.Value)));
                }

                root.Add(overrides);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString();
        }

        public static ConfigLoadResult<DatasheetMapping> FromXml(string xml)
        {
            var mapping = new DatasheetMapping();

            XDocument document;
            try
            {
                document = XDocument.Parse(xml);
            }
            catch (XmlException ex)
            {
                return new ConfigLoadResult<DatasheetMapping>(mapping,
                    new[] { ConfigIssue.Error($"The datasheet mapping is not well-formed XML: {ex.Message}") });
            }

            XElement? root = document.Root;
            if (root == null || root.Name.LocalName != "DatasheetMapping")
            {
                return new ConfigLoadResult<DatasheetMapping>(mapping,
                    new[] { ConfigIssue.Error("The root element must be <DatasheetMapping>.") });
            }

            if (Enum.TryParse((string?)root.Attribute("orientation"), out DatasheetOrientation orientation))
            {
                mapping.Orientation = orientation;
            }

            if (int.TryParse((string?)root.Attribute("headerRow"), out int headerRow))
            {
                mapping.HeaderRowIndex = headerRow;
            }

            if (int.TryParse((string?)root.Attribute("firstDataRow"), out int firstData))
            {
                mapping.FirstDataRowIndex = firstData;
            }

            if (int.TryParse((string?)root.Attribute("lastDataRow"), out int lastData))
            {
                mapping.LastDataRowIndex = lastData;
            }

            if (Enum.TryParse((string?)root.Attribute("defaultUnitSystem"), out UnitSystem unitSystem))
            {
                mapping.DefaultUnitSystem = unitSystem;
            }

            mapping.UnitColumnHeader = (string?)root.Attribute("unitColumnHeader");

            foreach (XElement header in root.Element("Headers")?.Elements("Header") ?? System.Linq.Enumerable.Empty<XElement>())
            {
                if (Enum.TryParse((string?)header.Attribute("parameter"), out DatasheetParameter parameter))
                {
                    mapping.Headers[parameter] = (string?)header.Attribute("text") ?? string.Empty;
                }
            }

            XElement? precision = root.Element("Precision");
            if (precision != null)
            {
                var policy = new PrecisionPolicy();
                if (Enum.TryParse((string?)precision.Attribute("mode"), out PrecisionMode mode))
                {
                    policy.Mode = mode;
                }

                if (int.TryParse((string?)precision.Attribute("digits"), out int digits))
                {
                    policy.Digits = digits;
                }

                if (Enum.TryParse((string?)precision.Attribute("rounding"), out RoundingMode rounding))
                {
                    policy.Rounding = rounding;
                }

                mapping.Precision = policy;
            }

            foreach (XElement over in root.Element("ResolutionOverrides")?.Elements("Override") ?? System.Linq.Enumerable.Empty<XElement>())
            {
                string systemId = (string?)over.Attribute("systemId") ?? string.Empty;
                string universalId = (string?)over.Attribute("universalId") ?? string.Empty;
                if (systemId.Length > 0 && universalId.Length > 0)
                {
                    mapping.ResolutionOverrides[systemId] = universalId;
                }
            }

            return new ConfigLoadResult<DatasheetMapping>(mapping, Array.Empty<ConfigIssue>());
        }
    }
}
