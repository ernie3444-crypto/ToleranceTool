using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using ToleranceTool.Configuration;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.Import
{
    /// <summary>
    /// Persists a resolved signal set (the join output) so the datasheet mapping
    /// pane can resolve System IDs without re-running the import. Stored as a
    /// workbook sidecar.
    /// </summary>
    public static class SignalConfigSetXml
    {
        private const string Root = "Signals";

        public static void Save(IEnumerable<SignalConfig> signals, string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var root = new XElement(Root);
            foreach (SignalConfig signal in signals)
            {
                root.Add(new XElement(
                    "Signal",
                    new XAttribute("universalId", signal.UniversalId),
                    new XAttribute("sensorName", signal.SensorName),
                    new XAttribute("conversionSense", signal.ConversionSense),
                    new XAttribute("scaleType", signal.ScaleType),
                    new XAttribute("signalType", signal.SignalType),
                    new XAttribute("moduleType", signal.ModuleType),
                    new XAttribute("rawLow", Num(signal.RawLow)),
                    new XAttribute("rawHigh", Num(signal.RawHigh)),
                    new XAttribute("euLow", Num(signal.EuLow)),
                    new XAttribute("euHigh", Num(signal.EuHigh)),
                    new XAttribute("euLowSi", Num(signal.EuLowSi)),
                    new XAttribute("euHighSi", Num(signal.EuHighSi))));
            }

            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(path);
        }

        public static ConfigLoadResult<List<SignalConfig>> Load(string path)
        {
            var signals = new List<SignalConfig>();
            if (!File.Exists(path))
            {
                return new ConfigLoadResult<List<SignalConfig>>(signals,
                    new[] { ConfigIssue.Error($"The signal set file was not found: {path}") });
            }

            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (XmlException ex)
            {
                return new ConfigLoadResult<List<SignalConfig>>(signals,
                    new[] { ConfigIssue.Error($"The signal set is not well-formed XML: {ex.Message}") });
            }

            foreach (XElement element in document.Root?.Elements("Signal") ?? System.Linq.Enumerable.Empty<XElement>())
            {
                var signal = new SignalConfig
                {
                    UniversalId = (string?)element.Attribute("universalId") ?? string.Empty,
                    SensorName = (string?)element.Attribute("sensorName") ?? string.Empty,
                    ScaleType = (string?)element.Attribute("scaleType") ?? string.Empty,
                    SignalType = (string?)element.Attribute("signalType") ?? string.Empty,
                    ModuleType = (string?)element.Attribute("moduleType") ?? string.Empty,
                    RawLow = Parse(element, "rawLow"),
                    RawHigh = Parse(element, "rawHigh"),
                    EuLow = Parse(element, "euLow"),
                    EuHigh = Parse(element, "euHigh"),
                    EuLowSi = Parse(element, "euLowSi"),
                    EuHighSi = Parse(element, "euHighSi"),
                };

                if (Enum.TryParse((string?)element.Attribute("conversionSense"), out ConversionSense sense))
                {
                    signal.ConversionSense = sense;
                }

                signals.Add(signal);
            }

            return new ConfigLoadResult<List<SignalConfig>>(signals, Array.Empty<ConfigIssue>());
        }

        private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static double Parse(XElement element, string name) =>
            double.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : 0;
    }
}
