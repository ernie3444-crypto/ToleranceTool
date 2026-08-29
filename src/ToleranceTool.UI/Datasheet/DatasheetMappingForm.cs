using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Aliases;
using ToleranceTool.Configuration.Datasheet;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Precision;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Excel.Datasheet;
using ToleranceTool.Import;

namespace ToleranceTool.UI.Datasheet
{
    /// <summary>
    /// The Datasheet Mapping pane (architecture doc §6/§10): bind headers, pick the
    /// unit system + precision, review System ID resolution per row, then Apply / Check.
    /// </summary>
    public sealed class DatasheetMappingForm : Form
    {
        private readonly IDatasheet _sheet;
        private readonly Action<string>? _persistMapping;

        private DatasheetMapping _mapping = new DatasheetMapping();
        private List<SignalConfig> _signals = new List<SignalConfig>();
        private ToleranceLibrary _tolerances = new ToleranceLibrary();
        private AliasTableSet _aliases = AliasTableSet.Empty();
        private readonly ScaleCurveLibrary _curves;

        private readonly ComboBox _orientation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly TextBox _headerRow = new TextBox { Width = 60, Text = "1" };
        private readonly Dictionary<DatasheetParameter, ComboBox> _headerCombos = new Dictionary<DatasheetParameter, ComboBox>();
        private readonly ComboBox _unitColumn = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        private readonly ComboBox _unitSystem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        private readonly ComboBox _precisionMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        private readonly TextBox _precisionDigits = new TextBox { Width = 50, Text = "3" };
        private readonly ComboBox _rounding = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };

        private readonly DataGridView _review = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };

        private readonly TextBox _report = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font(FontFamily.GenericMonospace, 8.5f) };
        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };

        public DatasheetMappingForm(IDatasheet sheet, string? mappingXml = null, Action<string>? persistMapping = null)
        {
            _sheet = sheet;
            _persistMapping = persistMapping;
            _curves = LoadCurves();

            Text = $"Datasheet Mapping — {sheet.Name}";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 640);
            MinimumSize = new Size(780, 540);

            _unitSystem.Items.AddRange(new object[] { UnitSystem.English, UnitSystem.Si });
            _unitSystem.SelectedIndex = 0;
            _precisionMode.Items.AddRange(new object[] { PrecisionMode.MatchExpected, PrecisionMode.SignificantFigures, PrecisionMode.DecimalPlaces });
            _precisionMode.SelectedIndex = 0;
            _rounding.Items.AddRange(new object[] { RoundingMode.HalfToEven, RoundingMode.HalfUp });
            _rounding.SelectedIndex = 0;

            _review.Columns.Add("Row", "Row");
            _review.Columns.Add("SystemId", "System ID");
            _review.Columns.Add("Step", "Resolved by");
            _review.Columns.Add("Signal", "Signal (Sensor Name)");
            _review.Columns.Add(new DataGridViewComboBoxColumn { Name = "Override", HeaderText = "Override → Universal ID" });

            Controls.Add(BuildBody());
            Controls.Add(_status);

            LoadConfig();
            if (!string.IsNullOrWhiteSpace(mappingXml))
            {
                _mapping = DatasheetMappingXml.FromXml(mappingXml!).Value;
            }

            PopulateHeaderChoices();
            ApplyMappingToControls();
            RefreshReview();
        }

        // --- layout --------------------------------------------------------

        private Control BuildBody()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 250 };
            split.Panel1.Controls.Add(BuildTopPanel());

            var bottom = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 600 };
            bottom.Panel1.Controls.Add(_review);
            bottom.Panel1.Controls.Add(new Label { Text = "Resolution review", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            bottom.Panel2.Controls.Add(_report);
            bottom.Panel2.Controls.Add(new Label { Text = "Run report", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            split.Panel2.Controls.Add(bottom);
            return split;
        }

        private Control BuildTopPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, Padding = new Padding(8) };

            void Row(string label, Control a, string? label2 = null, Control? b = null)
            {
                layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 8, 3) });
                layout.Controls.Add(a);
                layout.Controls.Add(new Label { Text = label2 ?? string.Empty, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 7, 8, 3) });
                layout.Controls.Add(b ?? new Label());
            }

            _orientation.Items.AddRange(new object[] { DatasheetOrientation.RowPerCase, DatasheetOrientation.ColumnPerCase });
            _orientation.SelectedIndex = 0;
            _orientation.SelectedIndexChanged += (s, e) => { PopulateHeaderChoices(); RefreshReview(); };
            Row("Orientation", _orientation, "Label row/col (1-based)", _headerRow);

            _headerRow.TextChanged += (s, e) => { PopulateHeaderChoices(); };

            foreach (DatasheetParameter parameter in Enum.GetValues(typeof(DatasheetParameter)).Cast<DatasheetParameter>())
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
                _headerCombos[parameter] = combo;
                Row(parameter.ToString(), combo);
            }

            Row("Per-row unit column", _unitColumn, "Default unit system", _unitSystem);
            Row("Precision", _precisionMode, "Digits", _precisionDigits);
            Row("Rounding", _rounding);

            var hint = new Label
            {
                Text = "Expected / Tolerance / Actual / Pass-Fail may repeat — each repeated group is another test point on the same row.",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Color.DimGray,
                Padding = new Padding(4, 4, 4, 4),
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36 };
            buttons.Controls.Add(Button("Load signal set…", LoadSignalSet));
            buttons.Controls.Add(Button("Refresh review", RefreshReview));
            buttons.Controls.Add(Button("Save mapping", SaveMapping));
            buttons.Controls.Add(Button("Check", () => Run(DatasheetRunMode.Check)));
            buttons.Controls.Add(Button("Apply", () => Run(DatasheetRunMode.Apply)));

            panel.Controls.Add(layout);
            panel.Controls.Add(hint);
            panel.Controls.Add(buttons);
            return panel;
        }

        // --- config -------------------------------------------------------

        private ScaleCurveLibrary LoadCurves()
        {
            try
            {
                if (File.Exists(ConfigurationPaths.ScaleTypeLibraryFile))
                {
                    var result = Configuration.Scales.ScaleTypeLibraryXml.Load(ConfigurationPaths.ScaleTypeLibraryFile);
                    if (!result.HasErrors && result.Value.Count > 0)
                    {
                        return ScaleCurveLibrary.From(result.Value);
                    }
                }
            }
            catch
            {
                // fall through to the built-ins
            }

            return ScaleCurveLibrary.CreateDefault();
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigurationPaths.ToleranceLibraryFile))
            {
                _tolerances = ToleranceLibraryXml.Load(ConfigurationPaths.ToleranceLibraryFile).Value;
            }

            if (File.Exists(ConfigurationPaths.AliasTablesFile))
            {
                _aliases = AliasTablesXml.Load(ConfigurationPaths.AliasTablesFile).Value;
            }

            string sidecar = Path.Combine(ConfigurationPaths.RootFolder, "last-signal-set.xml");
            if (File.Exists(sidecar))
            {
                _signals = SignalConfigSetXml.Load(sidecar).Value;
            }

            _status.Text = $"{_signals.Count} signal(s), {_tolerances.Count} tolerance(s), {_aliases.Tables.Count} alias table(s) loaded.";
        }

        private void LoadSignalSet()
        {
            using (var open = new OpenFileDialog { Filter = "Signal set (*.xml)|*.xml|All files (*.*)|*.*" })
            {
                if (open.ShowDialog(this) == DialogResult.OK)
                {
                    ConfigLoadResult<List<SignalConfig>> result = SignalConfigSetXml.Load(open.FileName);
                    _signals = result.Value;
                    _status.Text = $"Loaded {_signals.Count} signal(s) from {open.FileName}";
                    RefreshReview();
                }
            }
        }

        // --- mapping <-> controls ---------------------------------------

        private void PopulateHeaderChoices()
        {
            int headerRow = HeaderRowIndex();
            string?[] headers = SafeRow(headerRow);
            var options = headers.Select((h, i) => string.IsNullOrWhiteSpace(h) ? null : h!.Trim())
                .Where(h => h != null)
                .Cast<string>()
                .Distinct()
                .ToArray();

            foreach (ComboBox combo in _headerCombos.Values)
            {
                object? current = combo.SelectedItem;
                combo.Items.Clear();
                combo.Items.Add(string.Empty);
                combo.Items.AddRange(options);
                combo.SelectedItem = current;
            }

            _unitColumn.Items.Clear();
            _unitColumn.Items.Add(string.Empty);
            _unitColumn.Items.AddRange(options);
        }

        private void ApplyMappingToControls()
        {
            _orientation.SelectedItem = _mapping.Orientation;
            _headerRow.Text = (_mapping.HeaderRowIndex + 1).ToString();
            foreach (var pair in _headerCombos)
            {
                pair.Value.SelectedItem = _mapping.Header(pair.Key) ?? string.Empty;
            }

            _unitColumn.SelectedItem = _mapping.UnitColumnHeader ?? string.Empty;
            _unitSystem.SelectedItem = _mapping.DefaultUnitSystem;
            _precisionMode.SelectedItem = _mapping.Precision.Mode;
            _precisionDigits.Text = _mapping.Precision.Digits.ToString();
            _rounding.SelectedItem = _mapping.Precision.Rounding;
        }

        private DatasheetMapping ReadMappingFromControls()
        {
            var mapping = new DatasheetMapping
            {
                Orientation = SelectedOrientation(),
                HeaderRowIndex = HeaderRowIndex(),
                DefaultUnitSystem = (UnitSystem)_unitSystem.SelectedItem,
                UnitColumnHeader = Blank(_unitColumn.SelectedItem),
            };

            foreach (var pair in _headerCombos)
            {
                string? header = Blank(pair.Value.SelectedItem);
                if (header != null)
                {
                    mapping.Headers[pair.Key] = header;
                }
            }

            var policy = new PrecisionPolicy
            {
                Mode = (PrecisionMode)_precisionMode.SelectedItem,
                Rounding = (RoundingMode)_rounding.SelectedItem,
                Digits = int.TryParse(_precisionDigits.Text.Trim(), out int digits) ? digits : 3,
            };
            mapping.Precision = policy;

            foreach (var pair in _mapping.ResolutionOverrides)
            {
                mapping.ResolutionOverrides[pair.Key] = pair.Value;
            }

            ReadOverridesFromGrid(mapping);
            return mapping;
        }

        private void ReadOverridesFromGrid(DatasheetMapping mapping)
        {
            foreach (DataGridViewRow row in _review.Rows)
            {
                string systemId = Convert.ToString(row.Cells["SystemId"].Value) ?? string.Empty;
                string over = Convert.ToString(row.Cells["Override"].Value) ?? string.Empty;
                if (systemId.Length > 0 && over.Length > 0)
                {
                    mapping.ResolutionOverrides[systemId] = over;
                }
                else if (systemId.Length > 0)
                {
                    mapping.ResolutionOverrides.Remove(systemId);
                }
            }
        }

        private void SaveMapping()
        {
            _mapping = ReadMappingFromControls();
            string xml = DatasheetMappingXml.ToXml(_mapping);

            try
            {
                _persistMapping?.Invoke(xml);
                Directory.CreateDirectory(Path.Combine(ConfigurationPaths.RootFolder, "sheets"));
                File.WriteAllText(SheetMappingPath(), xml);
                _status.Text = $"Mapping saved for \"{_sheet.Name}\".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string SheetMappingPath() =>
            Path.Combine(ConfigurationPaths.RootFolder, "sheets", MakeSafe(_sheet.Name) + ".xml");

        // --- review + run ---------------------------------------------

        private SignalResolver BuildResolver(DatasheetMapping mapping) =>
            new SignalResolver(_signals, _aliases, mapping.ResolutionOverrides);

        private void RefreshReview()
        {
            DatasheetMapping mapping = ReadMappingFromControls();
            _review.Rows.Clear();

            string? systemIdHeader = mapping.Header(DatasheetParameter.SystemId);
            if (string.IsNullOrWhiteSpace(systemIdHeader))
            {
                _status.Text = "Map the System ID header to see the resolution review.";
                return;
            }

            IDatasheet sheet = EffectiveSheet();
            int headerRow = mapping.HeaderRowIndex;
            string?[] headers = SafeRow(headerRow);
            int systemIdColumn = Array.FindIndex(headers, h => string.Equals(h?.Trim(), systemIdHeader!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (systemIdColumn < 0)
            {
                _status.Text = $"No column has the header \"{systemIdHeader}\".";
                return;
            }

            var resolver = BuildResolver(mapping);
            var universalIds = _signals.Select(s => s.UniversalId).Where(id => id.Length > 0).Distinct().OrderBy(x => x).ToArray();
            var overrideColumn = (DataGridViewComboBoxColumn)_review.Columns["Override"];
            overrideColumn.Items.Clear();
            overrideColumn.Items.Add(string.Empty);
            overrideColumn.Items.AddRange(universalIds);

            int last = mapping.LastDataRowIndex ?? sheet.LastRowIndex;
            for (int row = headerRow + 1; row <= last; row++)
            {
                string? systemId = sheet.GetText(row, systemIdColumn)?.Trim();
                if (string.IsNullOrEmpty(systemId))
                {
                    continue;
                }

                SignalResolution resolution = resolver.Resolve(systemId!);
                string signalText = resolution.IsResolved
                    ? $"{resolution.Signal!.SensorName}  ({resolution.Signal.SignalType} / {resolution.Signal.ModuleType})"
                    : resolution.Step == ResolutionStep.Ambiguous
                        ? "ambiguous: " + string.Join(", ", resolution.Candidates)
                        : "unresolved";

                int gridRow = _review.Rows.Add(row + 1, systemId, resolution.Step, signalText, string.Empty);
                if (mapping.ResolutionOverrides.TryGetValue(systemId!, out string existingOverride))
                {
                    _review.Rows[gridRow].Cells["Override"].Value = existingOverride;
                }

                if (!resolution.IsResolved)
                {
                    _review.Rows[gridRow].DefaultCellStyle.BackColor = Color.MistyRose;
                }
                else if (resolution.Step == ResolutionStep.AutoMatch)
                {
                    _review.Rows[gridRow].DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
                }
            }

            _status.Text = $"{_review.Rows.Count} data row(s) reviewed.";
        }

        private void Run(DatasheetRunMode mode)
        {
            DatasheetMapping mapping = ReadMappingFromControls();
            _mapping = mapping;

            var runner = new DatasheetRunner(BuildResolver(mapping), _tolerances, _curves);
            DatasheetRunResult result = runner.Run(_sheet, mapping, mode);

            _report.Text = FormatReport(result);
            _status.Text = result.Summary();
            RefreshReview();
        }

        private static string FormatReport(DatasheetRunResult result)
        {
            var lines = new List<string> { result.Summary() };
            foreach (string warning in result.Warnings)
            {
                lines.Add("  ! " + warning);
            }

            lines.Add(string.Empty);
            if (!result.DidRun)
            {
                return string.Join(Environment.NewLine, lines);
            }

            bool multi = result.TestPointsPerRow > 1;
            foreach (RowOutcome row in result.Rows)
            {
                string calc = row.Calculated.HasValue ? row.Calculated.Value.ToString("0.######") : "—";
                string where = multi ? $"row {row.RowIndex + 1}.{row.TestPoint}" : $"row {row.RowIndex + 1}";
                lines.Add($"  {where,-9} {row.SystemId,-22} {row.Status,-14} {calc,12}   {row.Note}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        // --- helpers ------------------------------------------------

        private int HeaderRowIndex() =>
            int.TryParse(_headerRow.Text.Trim(), out int oneBased) && oneBased >= 1 ? oneBased - 1 : 0;

        private DatasheetOrientation SelectedOrientation() => (DatasheetOrientation)_orientation.SelectedItem;

        private IDatasheet EffectiveSheet() =>
            SelectedOrientation() == DatasheetOrientation.ColumnPerCase ? new TransposedDatasheet(_sheet) : _sheet;

        private string?[] SafeRow(int rowIndex)
        {
            try
            {
                return EffectiveSheet().Row(rowIndex);
            }
            catch
            {
                return Array.Empty<string?>();
            }
        }

        private static string? Blank(object? value)
        {
            string text = Convert.ToString(value)?.Trim() ?? string.Empty;
            return text.Length == 0 ? null : text;
        }

        private static string MakeSafe(string name) =>
            string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        private static Font Bold() => new Font(SystemFonts.DefaultFont, FontStyle.Bold);

        private static Button Button(string text, Action onClick)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (s, e) => onClick();
            return button;
        }
    }
}
