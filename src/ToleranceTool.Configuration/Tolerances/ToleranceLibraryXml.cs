using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.Configuration.Tolerances
{
    /// <summary>
    /// Reads and writes <c>tolerances.xml</c>. Structural only: it turns the file
    /// into <see cref="ToleranceDefinition"/>s and reports anything malformed.
    /// Semantic checks (expression bodies, variable names) live in
    /// <see cref="ToleranceLibraryValidator"/>.
    /// </summary>
    public static class ToleranceLibraryXml
    {
        private const string Root = "Tolerances";
        private const string DefinitionElement = "Tolerance";

        public static ConfigLoadResult<ToleranceLibrary> Load(string path)
        {
            if (!File.Exists(path))
            {
                return new ConfigLoadResult<ToleranceLibrary>(
                    new ToleranceLibrary(),
                    new[] { ConfigIssue.Error($"The tolerance library file was not found: {path}") });
            }

            using (var reader = new StreamReader(path))
            {
                return Load(reader);
            }
        }

        public static ConfigLoadResult<ToleranceLibrary> Load(TextReader reader)
        {
            var issues = new List<ConfigIssue>();
            var library = new ToleranceLibrary();

            XDocument document;
            try
            {
                document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            }
            catch (XmlException ex)
            {
                issues.Add(ConfigIssue.Error($"The tolerance library is not well-formed XML: {ex.Message}"));
                return new ConfigLoadResult<ToleranceLibrary>(library, issues);
            }

            XElement? root = document.Root;
            if (root == null || root.Name.LocalName != Root)
            {
                issues.Add(ConfigIssue.Error($"The root element must be <{Root}>."));
                return new ConfigLoadResult<ToleranceLibrary>(library, issues);
            }

            foreach (XElement element in root.Elements(DefinitionElement))
            {
                ToleranceDefinition? definition = ReadDefinition(element, issues);
                if (definition != null)
                {
                    library.AddRaw(definition);
                }
            }

            foreach (string key in library.DuplicateKeys())
            {
                issues.Add(ConfigIssue.Error("The library has more than one tolerance with this key.", key));
            }

            return new ConfigLoadResult<ToleranceLibrary>(library, issues);
        }

        public static void Save(ToleranceLibrary library, string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(path))
            {
                Save(library, writer);
            }
        }

        public static void Save(ToleranceLibrary library, TextWriter writer)
        {
            var root = new XElement(Root);

            foreach (ToleranceDefinition definition in library.Definitions)
            {
                var element = new XElement(
                    DefinitionElement,
                    new XAttribute("signalType", definition.SignalType),
                    new XAttribute("moduleType", definition.ModuleType));

                foreach (ToleranceTerm term in definition.Terms)
                {
                    element.Add(WriteTerm(term));
                }

                root.Add(element);
            }

            new XDocument(new XDeclaration("1.0", "utf-8", null), root)
                .Save(writer, SaveOptions.None);
        }

        // --- reading -------------------------------------------------------------

        private static ToleranceDefinition? ReadDefinition(XElement element, List<ConfigIssue> issues)
        {
            string signalType = (string?)element.Attribute("signalType") ?? string.Empty;
            string moduleType = (string?)element.Attribute("moduleType") ?? string.Empty;
            string scope = ToleranceLibrary.KeyOf(signalType, moduleType);

            if (string.IsNullOrWhiteSpace(signalType) || string.IsNullOrWhiteSpace(moduleType))
            {
                issues.Add(ConfigIssue.Error(
                    $"A <{DefinitionElement}> needs both a signalType and a moduleType.", Locate(element)));
                return null;
            }

            var definition = new ToleranceDefinition { SignalType = signalType.Trim(), ModuleType = moduleType.Trim() };

            foreach (XElement termElement in element.Elements())
            {
                ToleranceTerm? term = ReadTerm(termElement, scope, issues);
                if (term != null)
                {
                    definition.Terms.Add(term);
                }
            }

            if (definition.Terms.Count == 0)
            {
                issues.Add(ConfigIssue.Error("A tolerance must have at least one term.", scope));
                return null;
            }

            return definition;
        }

        private static ToleranceTerm? ReadTerm(XElement element, string scope, List<ConfigIssue> issues)
        {
            switch (element.Name.LocalName)
            {
                case "Percent":
                {
                    if (!TryReadDouble(element, "value", scope, issues, out double value))
                    {
                        return null;
                    }

                    if (!TryReadEnum(element, "basis", PercentBasis.RawSpan, PercentBasisNames, scope, issues, out PercentBasis basis))
                    {
                        return null;
                    }

                    ToleranceSpace defaultSpace = basis == PercentBasis.EuSpan ? ToleranceSpace.Eu : ToleranceSpace.Raw;
                    if (!TryReadEnum(element, "space", defaultSpace, SpaceNames, scope, issues, out ToleranceSpace space))
                    {
                        return null;
                    }

                    return new ToleranceTerm
                    {
                        Kind = ToleranceTermKind.Percent,
                        Value = value,
                        PercentBasis = basis,
                        Space = space,
                    };
                }

                case "AbsoluteEu":
                {
                    if (!TryReadDouble(element, "value", scope, issues, out double value))
                    {
                        return null;
                    }

                    if (!TryReadEnum(element, "unitSystem", UnitSystem.English, UnitSystemNames, scope, issues, out UnitSystem unitSystem))
                    {
                        return null;
                    }

                    return new ToleranceTerm
                    {
                        Kind = ToleranceTermKind.AbsoluteEu,
                        Value = value,
                        Unit = (string?)element.Attribute("unit") ?? string.Empty,
                        UnitSystem = unitSystem,
                    };
                }

                case "AbsoluteRaw":
                {
                    if (!TryReadDouble(element, "value", scope, issues, out double value))
                    {
                        return null;
                    }

                    return new ToleranceTerm
                    {
                        Kind = ToleranceTermKind.AbsoluteRaw,
                        Value = value,
                        Unit = (string?)element.Attribute("unit") ?? string.Empty,
                    };
                }

                case "Expression":
                {
                    string body = element.Value?.Trim() ?? string.Empty;
                    if (body.Length == 0)
                    {
                        issues.Add(ConfigIssue.Error("An <Expression> term has an empty body.", scope));
                        return null;
                    }

                    if (!TryReadEnum(element, "space", ToleranceSpace.Raw, SpaceNames, scope, issues, out ToleranceSpace space))
                    {
                        return null;
                    }

                    return new ToleranceTerm
                    {
                        Kind = ToleranceTermKind.Expression,
                        ExpressionBody = body,
                        Space = space,
                    };
                }

                default:
                    issues.Add(ConfigIssue.Error(
                        $"<{element.Name.LocalName}> is not a known tolerance term.", scope));
                    return null;
            }
        }

        // --- writing -----------------------------------------------------------

        private static XElement WriteTerm(ToleranceTerm term)
        {
            switch (term.Kind)
            {
                case ToleranceTermKind.Percent:
                {
                    var element = new XElement(
                        "Percent",
                        new XAttribute("value", Format(term.Value)),
                        new XAttribute("basis", PercentBasisNames[term.PercentBasis]));

                    ToleranceSpace defaultSpace = term.PercentBasis == PercentBasis.EuSpan ? ToleranceSpace.Eu : ToleranceSpace.Raw;
                    if (term.Space != defaultSpace)
                    {
                        element.Add(new XAttribute("space", SpaceNames[term.Space]));
                    }

                    return element;
                }

                case ToleranceTermKind.AbsoluteEu:
                {
                    var element = new XElement("AbsoluteEu", new XAttribute("value", Format(term.Value)));
                    if (!string.IsNullOrEmpty(term.Unit))
                    {
                        element.Add(new XAttribute("unit", term.Unit));
                    }

                    element.Add(new XAttribute("unitSystem", UnitSystemNames[term.UnitSystem]));
                    return element;
                }

                case ToleranceTermKind.AbsoluteRaw:
                {
                    var element = new XElement("AbsoluteRaw", new XAttribute("value", Format(term.Value)));
                    if (!string.IsNullOrEmpty(term.Unit))
                    {
                        element.Add(new XAttribute("unit", term.Unit));
                    }

                    return element;
                }

                case ToleranceTermKind.Expression:
                    return new XElement(
                        "Expression",
                        new XAttribute("space", SpaceNames[term.Space]),
                        term.ExpressionBody);

                default:
                    throw new ArgumentOutOfRangeException(nameof(term), term.Kind, "Unknown tolerance term kind.");
            }
        }

        // --- helpers ---------------------------------------------------------

        private static readonly Dictionary<PercentBasis, string> PercentBasisNames = new Dictionary<PercentBasis, string>
        {
            [PercentBasis.RawSpan] = "rawSpan",
            [PercentBasis.EuSpan] = "euSpan",
            [PercentBasis.Reading] = "reading",
        };

        private static readonly Dictionary<ToleranceSpace, string> SpaceNames = new Dictionary<ToleranceSpace, string>
        {
            [ToleranceSpace.Raw] = "raw",
            [ToleranceSpace.Eu] = "eu",
        };

        private static readonly Dictionary<UnitSystem, string> UnitSystemNames = new Dictionary<UnitSystem, string>
        {
            [UnitSystem.English] = "English",
            [UnitSystem.Si] = "SI",
        };

        private static bool TryReadDouble(
            XElement element, string attribute, string scope, List<ConfigIssue> issues, out double value)
        {
            value = 0;
            string? text = (string?)element.Attribute(attribute);
            if (text == null)
            {
                issues.Add(ConfigIssue.Error(
                    $"<{element.Name.LocalName}> is missing the required \"{attribute}\" attribute.", scope));
                return false;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                issues.Add(ConfigIssue.Error(
                    $"<{element.Name.LocalName}> has a \"{attribute}\" value that is not a number: \"{text}\".", scope));
                return false;
            }

            return true;
        }

        private static bool TryReadEnum<TEnum>(
            XElement element,
            string attribute,
            TEnum fallback,
            Dictionary<TEnum, string> names,
            string scope,
            List<ConfigIssue> issues,
            out TEnum value)
            where TEnum : struct
        {
            value = fallback;
            string? text = (string?)element.Attribute(attribute);
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string trimmed = text!.Trim();
            foreach (KeyValuePair<TEnum, string> pair in names)
            {
                if (string.Equals(pair.Value, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Key;
                    return true;
                }
            }

            issues.Add(ConfigIssue.Error(
                $"<{element.Name.LocalName}> has an unrecognized \"{attribute}\" value: \"{text}\". " +
                $"Expected one of: {string.Join(", ", names.Values)}.",
                scope));
            return false;
        }

        private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Locate(XElement element) =>
            element is IXmlLineInfo info && info.HasLineInfo() ? $"line {info.LineNumber}" : element.Name.LocalName;
    }
}
