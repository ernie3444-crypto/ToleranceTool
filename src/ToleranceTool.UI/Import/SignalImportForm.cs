using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.SignalTypes;
using ToleranceTool.Core.Signals;
using ToleranceTool.Import;
using ToleranceTool.Import.Access;

namespace ToleranceTool.UI.Import
{
    /// <summary>
    /// The file-based signal import wizard (architecture doc §7): add sources, pick
    /// the master, map each source's fields, then a live preview grid with a
    /// Complete flag per signal.
    /// </summary>
    public sealed class SignalImportForm : Form
    {
        private readonly List<ImportSourceDefinition> _sources = new List<ImportSourceDefinition>();

        private readonly ListBox _sourceList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly CheckBox _isMaster = new CheckBox { Text = "This source is the master (links Sensor Name → Universal ID)", AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
        private readonly ComboBox _orientation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, Anchor = AnchorStyles.Left };
        private readonly TextBox _sheetName = new TextBox { Width = 180, Anchor = AnchorStyles.Left };
        private readonly TextBox _headerRow = new TextBox { Width = 60, Text = "1", Anchor = AnchorStyles.Left };
        private readonly TextBox _keyColumn = new TextBox { Width = 120, Text = "A", Anchor = AnchorStyles.Left };
        private readonly TextBox _paramNameCol = new TextBox { Width = 120, Anchor = AnchorStyles.Left };
        private readonly TextBox _paramValueCol = new TextBox { Width = 120, Anchor = AnchorStyles.Left };
        private readonly TextBox _paramMetricCol = new TextBox { Width = 120, Anchor = AnchorStyles.Left };

        private readonly DataGridView _mapping = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EditMode = DataGridViewEditMode.EditOnEnter,
        };

        private readonly DataGridView _preview = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
        };

        private readonly ListBox _issues = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
        private readonly CheckBox _hideIncomplete = new CheckBox { Text = "Hide incomplete", AutoSize = true, Margin = new Padding(12, 8, 3, 3) };

        private int _shownIndex = -1;
        private bool _suspend;

        private SplitContainer _outerSplit = null!;
        private SplitContainer _topSplit = null!;
        private SplitContainer _bottomSplit = null!;

        public SignalImportForm()
        {
            Text = "Signal Configuration Import";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1040, 700);
            MinimumSize = new Size(860, 580);

            BuildMappingColumns();

            _orientation.Items.AddRange(new object[]
            {
                SignalDataOrientation.RowPerSignal,
                SignalDataOrientation.ColumnPerSignal,
                SignalDataOrientation.ParameterPerRow,
            });
            _orientation.SelectedIndex = 0;

            _sourceList.SelectedIndexChanged += (s, e) => OnSourceSelectionChanged();
            _isMaster.CheckedChanged += (s, e) => OnMasterToggled();
            _orientation.SelectedIndexChanged += (s, e) => { if (!_suspend) { CommitShownSource(); LoadShownSource(); } };
            _sheetName.TextChanged += (s, e) => CommitShownSource();
            _headerRow.TextChanged += (s, e) => CommitShownSource();
            _keyColumn.TextChanged += (s, e) => CommitShownSource();
            _paramNameCol.TextChanged += (s, e) => CommitShownSource();
            _paramValueCol.TextChanged += (s, e) => CommitShownSource();
            _paramMetricCol.TextChanged += (s, e) => CommitShownSource();
            _hideIncomplete.CheckedChanged += (s, e) => { if (_preview.Columns.Count > 0) BuildPreview(); };

            Controls.Add(BuildBody());
            Controls.Add(_status);

            ResultSet = new ResolvedSignalSet(Array.Empty<ResolvedSignal>());
            SetEditorEnabled(false);

            LoadSavedSources();
        }

        private void LoadSavedSources()
        {
            try
            {
                List<ImportSourceDefinition> saved = ImportSourceDefinitionsXml.Load(ConfigurationPaths.ImportSourcesFile);
                if (saved.Count == 0)
                {
                    return;
                }

                _sources.AddRange(saved);
                RefreshSourceList(select: 0);
                _status.Text = $"Loaded {saved.Count} saved source(s). Missing files are reported when you build the preview.";
            }
            catch
            {
                // start empty on any load problem
            }
        }

        private void SaveSources()
        {
            try
            {
                CommitShownSource();
                Directory.CreateDirectory(ConfigurationPaths.RootFolder);
                ImportSourceDefinitionsXml.Save(_sources, ConfigurationPaths.ImportSourcesFile);
            }
            catch
            {
                // non-fatal — the setup just will not persist this time
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSources();
            base.OnFormClosing(e);
        }

        /// <summary>The last preview build (all signals). Only the complete ones are saved / used downstream.</summary>
        public ResolvedSignalSet ResultSet { get; private set; }

        // --- layout ----------------------------------------------------------

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            TrySetSplit(_outerSplit, 0.48);
            TrySetSplit(_topSplit, 0.30);
            TrySetSplit(_bottomSplit, 0.70);
        }

        private static void TrySetSplit(SplitContainer split, double fraction)
        {
            try
            {
                int extent = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
                int distance = (int)(extent * fraction);
                distance = Math.Max(split.Panel1MinSize, Math.Min(distance, extent - split.Panel2MinSize));
                if (distance > 0)
                {
                    split.SplitterDistance = distance;
                }
            }
            catch (InvalidOperationException)
            {
                // window too small at load time; the default split is acceptable
            }
        }

        private Control BuildBody()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _outerSplit = split;

            var top = new SplitContainer { Dock = DockStyle.Fill };
            _topSplit = top;

            // left: source list
            var sourcesPanel = new Panel { Dock = DockStyle.Fill };
            var sourceButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 };
            sourceButtons.Controls.Add(Button("Add file…", AddFile));
            sourceButtons.Controls.Add(Button("Add Access…", AddAccessSource));
            sourceButtons.Controls.Add(Button("Remove", RemoveSource));
            sourcesPanel.Controls.Add(_sourceList);
            sourcesPanel.Controls.Add(sourceButtons);
            sourcesPanel.Controls.Add(new Label { Text = "Sources", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            top.Panel1.Controls.Add(sourcesPanel);

            // right: field mapping editor for the selected source
            var mapPanel = new Panel { Dock = DockStyle.Fill };
            mapPanel.Controls.Add(_mapping);                    // added first -> fills remaining space
            mapPanel.Controls.Add(new Label
            {
                Text = "Row per signal: give each field its column.   Column per signal: give each field its row number.\n" +
                       "Parameter per row: give each field the text that identifies it in the Parameter-name column; SI range values come from the metric column.",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Color.DimGray,
                Padding = new Padding(4, 2, 4, 4),
            });
            mapPanel.Controls.Add(BuildSettingsPanel());        // docks above the grid
            mapPanel.Controls.Add(new Label { Text = "Field mapping for selected source", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            top.Panel2.Controls.Add(mapPanel);

            var bottom = new SplitContainer { Dock = DockStyle.Fill };
            _bottomSplit = bottom;

            var previewPanel = new Panel { Dock = DockStyle.Fill };
            var previewButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
            previewButtons.Controls.Add(Button("Build preview", BuildPreview));
            previewButtons.Controls.Add(Button("Save for datasheet use", SaveSignalSet));
            previewButtons.Controls.Add(Button("Export as…", ExportSignalSet));
            previewButtons.Controls.Add(_hideIncomplete);
            previewPanel.Controls.Add(_preview);
            previewPanel.Controls.Add(previewButtons);
            previewPanel.Controls.Add(new Label { Text = "Preview  (pink = incomplete, excluded from the saved set)", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            bottom.Panel1.Controls.Add(previewPanel);

            bottom.Panel2.Controls.Add(_issues);
            bottom.Panel2.Controls.Add(new Label { Text = "Issues", Dock = DockStyle.Top, Height = 20, Font = Bold() });

            split.Panel1.Controls.Add(top);
            split.Panel2.Controls.Add(bottom);
            return split;
        }

        private Control BuildSettingsPanel()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(6, 4, 6, 8),
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            table.Controls.Add(_isMaster, 0, 0);
            table.SetColumnSpan(_isMaster, 2);

            int r = 1;
            void Row(string label, Control control)
            {
                table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 12, 3) }, 0, r);
                table.Controls.Add(control, 1, r);
                control.Margin = new Padding(3, 4, 3, 4);
                r++;
            }

            Row("Layout", _orientation);
            Row("Worksheet (xlsx only)", _sheetName);
            Row("Header row (1-based)", _headerRow);
            Row("Unique ID column", _keyColumn);
            Row("Parameter-name column", _paramNameCol);
            Row("Parameter-value column", _paramValueCol);
            Row("Metric-value column (SI, optional)", _paramMetricCol);
            return table;
        }

        private void BuildMappingColumns()
        {
            _mapping.Columns.Add(new DataGridViewTextBoxColumn { Name = "Field", HeaderText = "Field", ReadOnly = true, FillWeight = 34 });
            _mapping.Columns.Add(new DataGridViewTextBoxColumn { Name = "Column", HeaderText = "Column / number", FillWeight = 34 });
            _mapping.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Required", HeaderText = "Required (blank ⇒ drop signal)", FillWeight = 32 });
        }

        // --- sources --------------------------------------------------------

        private void AddFile()
        {
            using (var open = new OpenFileDialog
            {
                Filter = "Signal source (*.csv;*.tsv;*.xlsx;*.xls)|*.csv;*.tsv;*.xlsx;*.xls|All files (*.*)|*.*",
                Multiselect = true,
            })
            {
                if (open.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                CommitShownSource();
                foreach (string path in open.FileNames)
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    SignalSourceKind kind = ext == ".csv" || ext == ".tsv"
                        ? SignalSourceKind.DelimitedText
                        : SignalSourceKind.Workbook;

                    var definition = new ImportSourceDefinition(Path.GetFileName(path), kind, path)
                    {
                        IsMaster = _sources.Count == 0,
                    };

                    foreach (SignalField field in SignalField.All)
                    {
                        definition.Fields.Add(new FieldBinding(field.Name, string.Empty, field.RequiredByDefault));
                    }

                    _sources.Add(definition);
                }

                RefreshSourceList(select: _sources.Count - 1);
            }
        }

        private void AddAccessSource()
        {
            using (var dialog = new AddAccessSourceDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                CommitShownSource();
                ImportSourceDefinition definition = dialog.BuildDefinition();
                if (definition.IsMaster || _sources.Count == 0)
                {
                    definition.IsMaster = true;
                    _sources.ForEach(s => s.IsMaster = false);
                }

                _sources.Add(definition);
                RefreshSourceList(select: _sources.Count - 1);
            }
        }

        private void RemoveSource()
        {
            int index = _sourceList.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            _sources.RemoveAt(index);
            _shownIndex = -1;
            RefreshSourceList(select: Math.Min(index, _sources.Count - 1));
        }

        private void RefreshSourceList(int select)
        {
            _suspend = true;
            _sourceList.BeginUpdate();
            _sourceList.Items.Clear();
            foreach (ImportSourceDefinition source in _sources)
            {
                _sourceList.Items.Add(Label(source));
            }

            _sourceList.EndUpdate();
            if (select >= 0 && select < _sourceList.Items.Count)
            {
                _sourceList.SelectedIndex = select;
            }

            _suspend = false;

            _shownIndex = _sourceList.SelectedIndex;
            LoadShownSource();
        }

        private void RefreshSelectedLabel()
        {
            if (_shownIndex >= 0 && _shownIndex < _sourceList.Items.Count)
            {
                _suspend = true;
                _sourceList.Items[_shownIndex] = Label(_sources[_shownIndex]);
                _suspend = false;
            }
        }

        private static string Label(ImportSourceDefinition source) =>
            source.IsMaster ? $"{source.Name}   [master]" : source.Name;

        // --- editor <-> definition ----------------------------------------

        private void OnSourceSelectionChanged()
        {
            if (_suspend)
            {
                return;
            }

            CommitShownSource();
            _shownIndex = _sourceList.SelectedIndex;
            LoadShownSource();
        }

        private void OnMasterToggled()
        {
            if (_suspend || _shownIndex < 0 || _shownIndex >= _sources.Count)
            {
                return;
            }

            ImportSourceDefinition source = _sources[_shownIndex];
            source.IsMaster = _isMaster.Checked;
            if (_isMaster.Checked)
            {
                foreach (ImportSourceDefinition other in _sources.Where(s => !ReferenceEquals(s, source)))
                {
                    other.IsMaster = false;
                }
            }

            _suspend = true;
            for (int i = 0; i < _sources.Count; i++)
            {
                _sourceList.Items[i] = Label(_sources[i]);
            }

            _suspend = false;

            LoadShownSource(); // the Sensor Name row appears / disappears with master
        }

        private void SetEditorEnabled(bool enabled)
        {
            _isMaster.Enabled = _orientation.Enabled = _sheetName.Enabled = _headerRow.Enabled =
                _keyColumn.Enabled = _paramNameCol.Enabled = _paramValueCol.Enabled = _paramMetricCol.Enabled =
                _mapping.Enabled = enabled;
        }

        private void LoadShownSource()
        {
            _suspend = true;
            _mapping.Rows.Clear();

            if (_shownIndex < 0 || _shownIndex >= _sources.Count)
            {
                SetEditorEnabled(false);
                _suspend = false;
                return;
            }

            SetEditorEnabled(true);
            ImportSourceDefinition source = _sources[_shownIndex];
            bool eav = source.Orientation == SignalDataOrientation.ParameterPerRow;

            _isMaster.Checked = source.IsMaster;
            _orientation.SelectedItem = source.Orientation;
            _sheetName.Text = source.SheetName ?? string.Empty;
            _headerRow.Text = source.HeaderRowIndex.HasValue ? (source.HeaderRowIndex.Value + 1).ToString() : string.Empty;
            _keyColumn.Text = source.UniversalIdLocator;
            _paramNameCol.Text = source.ParameterNameLocator ?? string.Empty;
            _paramValueCol.Text = source.ParameterValueLocator ?? string.Empty;
            _paramMetricCol.Text = source.ParameterMetricLocator ?? string.Empty;

            _orientation.Enabled = source.Kind != SignalSourceKind.Access;
            _sheetName.Enabled = source.Kind == SignalSourceKind.Workbook;
            _headerRow.Enabled = source.Kind != SignalSourceKind.Access;
            _paramNameCol.Enabled = _paramValueCol.Enabled = _paramMetricCol.Enabled = eav;
            _mapping.Columns["Column"].HeaderText = eav ? "Parameter name" : "Column / number";

            foreach (SignalField field in SignalField.All)
            {
                if (field.MasterOnly && !source.IsMaster)
                {
                    continue;
                }

                // In the parameter-per-row layout the SI-range fields are filled from
                // the metric column, not mapped directly.
                if (eav && (field.Name == SignalField.EuLowSi || field.Name == SignalField.EuHighSi))
                {
                    continue;
                }

                FieldBinding binding = source.Binding(field.Name)
                    ?? new FieldBinding(field.Name, string.Empty, field.RequiredByDefault);
                int row = _mapping.Rows.Add(field.Name, binding.Locator, binding.Required);
                _mapping.Rows[row].Tag = field.Name;
            }

            _suspend = false;
        }

        /// <summary>Reads the editor controls into the definition currently shown. Safe to call any time.</summary>
        private void CommitShownSource()
        {
            if (_suspend || _shownIndex < 0 || _shownIndex >= _sources.Count)
            {
                return;
            }

            ImportSourceDefinition source = _sources[_shownIndex];

            _mapping.EndEdit();

            source.IsMaster = _isMaster.Checked;
            if (_orientation.SelectedItem is SignalDataOrientation orientation)
            {
                source.Orientation = orientation;
            }

            source.SheetName = string.IsNullOrWhiteSpace(_sheetName.Text) ? null : _sheetName.Text.Trim();
            source.HeaderRowIndex = int.TryParse(_headerRow.Text.Trim(), out int header) && header >= 1 ? header - 1 : (int?)null;
            source.UniversalIdLocator = _keyColumn.Text.Trim();
            source.ParameterNameLocator = Blank(_paramNameCol.Text);
            source.ParameterValueLocator = Blank(_paramValueCol.Text);
            source.ParameterMetricLocator = Blank(_paramMetricCol.Text);

            foreach (DataGridViewRow gridRow in _mapping.Rows)
            {
                if (!(gridRow.Tag is string fieldName))
                {
                    continue;
                }

                FieldBinding? binding = source.Binding(fieldName);
                if (binding == null)
                {
                    binding = new FieldBinding(fieldName, string.Empty, false);
                    source.Fields.Add(binding);
                }

                binding.Locator = Convert.ToString(gridRow.Cells["Column"].Value)?.Trim() ?? string.Empty;
                binding.Required = gridRow.Cells["Required"].Value is bool b && b;
            }

            RefreshSelectedLabel();
        }

        // --- preview -------------------------------------------------------

        private void BuildPreview()
        {
            CommitShownSource();

            _issues.Items.Clear();
            _preview.Rows.Clear();
            _preview.Columns.Clear();

            if (_sources.Count == 0)
            {
                _issues.Items.Add("Add at least one source.");
                return;
            }

            var builder = new SignalSetBuilder();

            SignalTypeRegistry registry = File.Exists(ConfigurationPaths.SignalTypeRegistryFile)
                ? SignalTypeRegistryXml.Load(ConfigurationPaths.SignalTypeRegistryFile).Value
                : new SignalTypeRegistry();

            if (registry.Count > 0)
            {
                builder.WithRegistry(registry);
            }
            else if (!_sources.Any(s => s.Binding(SignalField.RawLow)?.Locator.Length > 0))
            {
                _issues.Items.Add(
                    "No signal-type registry — raw ranges will be blank, so raw-space tolerances cannot be calculated. " +
                    "Open Setup → Signal Types to add your signal types (name + raw low/high), or map Raw Low / Raw High here.");
            }

            foreach (ImportSourceDefinition definition in _sources)
            {
                var effective = new ImportSourceDefinition(definition.Name, definition.Kind, definition.Location)
                {
                    SheetName = definition.SheetName,
                    Orientation = definition.Orientation,
                    HeaderRowIndex = definition.HeaderRowIndex,
                    UniversalIdLocator = definition.UniversalIdLocator,
                    IsMaster = definition.IsMaster,
                    Query = definition.Query,
                    ParameterNameLocator = definition.ParameterNameLocator,
                    ParameterValueLocator = definition.ParameterValueLocator,
                    ParameterMetricLocator = definition.ParameterMetricLocator,
                };
                effective.Fields.AddRange(definition.Fields.Where(b => !string.IsNullOrWhiteSpace(b.Locator)));

                try
                {
                    ISignalSource source = effective.Kind == SignalSourceKind.Access
                        ? new AccessSignalSource(effective)
                        : FileSignalSource.Open(effective);
                    builder.Add(source, effective);
                }
                catch (Exception ex)
                {
                    _issues.Items.Add($"{definition.Name}: {ex.Message}");
                    return;
                }
            }

            ConfigLoadResult<ResolvedSignalSet> result = builder.Build();
            ResultSet = result.Value;

            foreach (ConfigIssue issue in result.Issues)
            {
                _issues.Items.Add(issue.ToString());
            }

            foreach (FieldGap gap in result.Value.AllGaps)
            {
                _issues.Items.Add("Dropped — " + gap);
            }

            FillPreviewGrid(result.Value);

            int complete = result.Value.Complete.Count();
            int dropped = result.Value.Count - complete;
            _status.Text = dropped == 0
                ? $"{complete} signal(s), all complete."
                : $"{complete} complete, {dropped} dropped (missing a required field — see Issues).";
            if (_issues.Items.Count == 0)
            {
                _issues.Items.Add(_status.Text);
            }

            SaveSources();
        }

        private void FillPreviewGrid(ResolvedSignalSet set)
        {
            string[] columns =
            {
                "Universal ID", "Sensor Name", "Conv.", "Scale", "Signal", "Module",
                "Raw Lo", "Raw Hi", "EU Lo", "EU Hi", "EU Lo SI", "EU Hi SI", "Complete", "Missing",
            };
            foreach (string column in columns)
            {
                _preview.Columns.Add(column, column);
            }

            IEnumerable<ResolvedSignal> rows = _hideIncomplete.Checked ? set.Complete : set.Signals;
            foreach (ResolvedSignal signal in rows.OrderBy(s => s.IsComplete ? 0 : 1))
            {
                SignalConfig c = signal.Config;
                string missing = signal.IsComplete ? string.Empty : string.Join(", ", signal.Gaps.Select(g => g.Field));
                int row = _preview.Rows.Add(
                    c.UniversalId, c.SensorName, c.ConversionSense, c.ScaleType, c.SignalType, c.ModuleType,
                    Num(c.RawLow), Num(c.RawHigh), Num(c.EuLow), Num(c.EuHigh), Num(c.EuLowSi), Num(c.EuHighSi),
                    signal.IsComplete ? "yes" : "no", missing);

                if (!signal.IsComplete)
                {
                    _preview.Rows[row].DefaultCellStyle.BackColor = Color.MistyRose;
                }
            }
        }

        private bool HasCompleteSignals()
        {
            if (ResultSet.Complete.Any())
            {
                return true;
            }

            _issues.Items.Add("Build the preview first (and make sure at least one signal is complete).");
            return false;
        }

        /// <summary>Writes the resolved set to the standard location the datasheet run reads.</summary>
        private void SaveSignalSet()
        {
            if (!HasCompleteSignals())
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigurationPaths.RootFolder);
                SignalConfigSetXml.Save(ResultSet.Complete.Select(s => s.Config), ConfigurationPaths.ResolvedSignalSetFile);
                SaveSources();
                _status.Text =
                    $"Saved {ResultSet.Complete.Count()} signal(s) for datasheet use — Apply / Check will use these.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportSignalSet()
        {
            if (!HasCompleteSignals())
            {
                return;
            }

            using (var save = new SaveFileDialog { Filter = "Signal set (*.xml)|*.xml", FileName = "signal-set.xml" })
            {
                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    SignalConfigSetXml.Save(ResultSet.Complete.Select(s => s.Config), save.FileName);
                    _status.Text = $"Exported {ResultSet.Complete.Count()} signal(s) to {save.FileName}";
                }
            }
        }

        // --- helpers -----------------------------------------------------

        private static string? Blank(string? text)
        {
            string trimmed = text?.Trim() ?? string.Empty;
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string Num(double value) => value == 0 ? "—" : value.ToString("0.######");

        private static Font Bold() => new Font(SystemFonts.DefaultFont, FontStyle.Bold);

        private static Button Button(string text, Action onClick)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
            button.Click += (s, e) => onClick();
            return button;
        }
    }
}
