using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Core.Signals;
using ToleranceTool.Import;

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
        private readonly CheckBox _isMaster = new CheckBox { Text = "This source is the master (links Sensor Name → Universal ID)", AutoSize = true };
        private readonly TextBox _sheetName = new TextBox { Width = 160 };
        private readonly TextBox _headerRow = new TextBox { Width = 60, Text = "1" };
        private readonly TextBox _keyColumn = new TextBox { Width = 60, Text = "A" };
        private readonly DataGridView _mapping = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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

        private bool _loadingSource;

        public SignalImportForm()
        {
            Text = "Signal Configuration Import";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 660);
            MinimumSize = new Size(820, 560);

            BuildMappingColumns();
            _sourceList.SelectedIndexChanged += (s, e) => LoadSelectedSource();
            _isMaster.CheckedChanged += (s, e) => WriteBackSource();
            _sheetName.TextChanged += (s, e) => WriteBackSource();
            _headerRow.TextChanged += (s, e) => WriteBackSource();
            _keyColumn.TextChanged += (s, e) => WriteBackSource();
            _mapping.CellEndEdit += (s, e) => WriteBackSource();
            _mapping.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_mapping.IsCurrentCellDirty)
                {
                    _mapping.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            Controls.Add(BuildBody());
            Controls.Add(_status);

            ResultSet = new ResolvedSignalSet(Array.Empty<ResolvedSignal>());
        }

        /// <summary>The last preview build. Consumed by the caller when the dialog closes with OK.</summary>
        public ResolvedSignalSet ResultSet { get; private set; }

        // --- layout ----------------------------------------------------------

        private Control BuildBody()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 360 };

            var top = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 300 };

            var sourcesPanel = new Panel { Dock = DockStyle.Fill };
            var sourceButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32 };
            sourceButtons.Controls.Add(Button("Add file…", AddFile));
            sourceButtons.Controls.Add(Button("Remove", RemoveSource));
            sourcesPanel.Controls.Add(_sourceList);
            sourcesPanel.Controls.Add(sourceButtons);
            sourcesPanel.Controls.Add(new Label { Text = "Sources", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            top.Panel1.Controls.Add(sourcesPanel);

            var mapPanel = new Panel { Dock = DockStyle.Fill };
            var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, WrapContents = true };
            header.Controls.Add(_isMaster);
            header.Controls.Add(new Label { Text = "  Sheet:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            header.Controls.Add(_sheetName);
            header.Controls.Add(new Label { Text = "  Header row:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            header.Controls.Add(_headerRow);
            header.Controls.Add(new Label { Text = "  Universal ID column:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            header.Controls.Add(_keyColumn);
            mapPanel.Controls.Add(_mapping);
            mapPanel.Controls.Add(header);
            mapPanel.Controls.Add(new Label { Text = "Field mapping for selected source", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            top.Panel2.Controls.Add(mapPanel);

            var bottom = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 700 };

            var previewPanel = new Panel { Dock = DockStyle.Fill };
            var previewButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 };
            previewButtons.Controls.Add(Button("Build preview", BuildPreview));
            var useButton = Button("Use this set", () => { DialogResult = DialogResult.OK; Close(); });
            previewButtons.Controls.Add(useButton);
            previewButtons.Controls.Add(Button("Save signal set…", SaveSignalSet));
            previewPanel.Controls.Add(_preview);
            previewPanel.Controls.Add(previewButtons);
            previewPanel.Controls.Add(new Label { Text = "Preview", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            bottom.Panel1.Controls.Add(previewPanel);

            bottom.Panel2.Controls.Add(_issues);
            bottom.Panel2.Controls.Add(new Label { Text = "Issues", Dock = DockStyle.Top, Height = 20, Font = Bold() });

            split.Panel1.Controls.Add(top);
            split.Panel2.Controls.Add(bottom);
            return split;
        }

        private void BuildMappingColumns()
        {
            _mapping.Columns.Add(new DataGridViewTextBoxColumn { Name = "Field", HeaderText = "Field", ReadOnly = true, FillWeight = 40 });
            _mapping.Columns.Add(new DataGridViewTextBoxColumn { Name = "Column", HeaderText = "Column / number", FillWeight = 35 });
            _mapping.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Required", HeaderText = "Required", FillWeight = 25 });
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

                foreach (string path in open.FileNames)
                {
                    SignalSourceKind kind = Path.GetExtension(path).ToLowerInvariant() == ".csv"
                        || Path.GetExtension(path).ToLowerInvariant() == ".tsv"
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

                RefreshSourceList();
                _sourceList.SelectedIndex = _sourceList.Items.Count - 1;
            }
        }

        private void RemoveSource()
        {
            int index = _sourceList.SelectedIndex;
            if (index >= 0)
            {
                _sources.RemoveAt(index);
                RefreshSourceList();
            }
        }

        private void RefreshSourceList()
        {
            _sourceList.BeginUpdate();
            _sourceList.Items.Clear();
            foreach (ImportSourceDefinition source in _sources)
            {
                _sourceList.Items.Add(source.IsMaster ? $"{source.Name}   [master]" : source.Name);
            }

            _sourceList.EndUpdate();
            LoadSelectedSource();
        }

        private ImportSourceDefinition? Current =>
            _sourceList.SelectedIndex >= 0 && _sourceList.SelectedIndex < _sources.Count
                ? _sources[_sourceList.SelectedIndex]
                : null;

        private void LoadSelectedSource()
        {
            ImportSourceDefinition? source = Current;
            _mapping.Rows.Clear();
            bool enabled = source != null;
            _isMaster.Enabled = _sheetName.Enabled = _headerRow.Enabled = _keyColumn.Enabled = _mapping.Enabled = enabled;
            if (source == null)
            {
                return;
            }

            _loadingSource = true;
            _isMaster.Checked = source.IsMaster;
            _sheetName.Text = source.SheetName ?? string.Empty;
            _headerRow.Text = source.HeaderRowIndex.HasValue ? (source.HeaderRowIndex.Value + 1).ToString() : string.Empty;
            _keyColumn.Text = source.UniversalIdLocator;

            foreach (SignalField field in SignalField.All)
            {
                if (field.MasterOnly && !source.IsMaster)
                {
                    continue;
                }

                FieldBinding binding = source.Binding(field.Name) ?? new FieldBinding(field.Name, string.Empty, field.RequiredByDefault);
                int row = _mapping.Rows.Add(field.Name, binding.Locator, binding.Required);
                _mapping.Rows[row].Tag = field.Name;
            }

            _loadingSource = false;
        }

        private void WriteBackSource()
        {
            if (_loadingSource)
            {
                return;
            }

            ImportSourceDefinition? source = Current;
            if (source == null)
            {
                return;
            }

            bool wasMaster = source.IsMaster;
            source.IsMaster = _isMaster.Checked;
            if (source.IsMaster && !wasMaster)
            {
                foreach (ImportSourceDefinition other in _sources.Where(s => !ReferenceEquals(s, source)))
                {
                    other.IsMaster = false;
                }
            }

            source.SheetName = string.IsNullOrWhiteSpace(_sheetName.Text) ? null : _sheetName.Text.Trim();
            source.HeaderRowIndex = int.TryParse(_headerRow.Text.Trim(), out int header) && header >= 1 ? header - 1 : (int?)null;
            source.UniversalIdLocator = _keyColumn.Text.Trim();

            foreach (DataGridViewRow gridRow in _mapping.Rows)
            {
                if (gridRow.Tag is not string fieldName)
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

            int selected = _sourceList.SelectedIndex;
            RefreshSourceListLabelsOnly();
            _sourceList.SelectedIndex = selected;
        }

        private void RefreshSourceListLabelsOnly()
        {
            for (int i = 0; i < _sources.Count && i < _sourceList.Items.Count; i++)
            {
                _sourceList.Items[i] = _sources[i].IsMaster ? $"{_sources[i].Name}   [master]" : _sources[i].Name;
            }
        }

        // --- preview -------------------------------------------------------

        private void BuildPreview()
        {
            _issues.Items.Clear();
            _preview.Rows.Clear();
            _preview.Columns.Clear();

            if (_sources.Count == 0)
            {
                _issues.Items.Add("Add at least one source.");
                return;
            }

            var builder = new SignalSetBuilder();
            foreach (ImportSourceDefinition definition in _sources)
            {
                var bindings = definition.Fields.Where(b => !string.IsNullOrWhiteSpace(b.Locator)).ToList();
                var effective = new ImportSourceDefinition(definition.Name, definition.Kind, definition.Location)
                {
                    SheetName = definition.SheetName,
                    Orientation = definition.Orientation,
                    HeaderRowIndex = definition.HeaderRowIndex,
                    UniversalIdLocator = definition.UniversalIdLocator,
                    IsMaster = definition.IsMaster,
                };
                effective.Fields.AddRange(bindings);

                try
                {
                    builder.Add(FileSignalSource.Open(effective), effective);
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
                _issues.Items.Add("Incomplete — " + gap);
            }

            if (_issues.Items.Count == 0)
            {
                _issues.Items.Add(result.Value.IsReady
                    ? $"Ready: {result.Value.Count} signal(s), all complete."
                    : "No issues, but the set is empty.");
            }

            FillPreviewGrid(result.Value);
            _status.Text = $"{result.Value.Count} signal(s) — {result.Value.Complete.Count()} complete";
        }

        private void FillPreviewGrid(ResolvedSignalSet set)
        {
            string[] columns =
            {
                "Universal ID", "Sensor Name", "Conv.", "Scale", "Signal", "Module",
                "Raw Lo", "Raw Hi", "EU Lo", "EU Hi", "EU Lo SI", "EU Hi SI", "Complete",
            };
            foreach (string column in columns)
            {
                _preview.Columns.Add(column, column);
            }

            foreach (ResolvedSignal signal in set.Signals)
            {
                SignalConfig c = signal.Config;
                int row = _preview.Rows.Add(
                    c.UniversalId, c.SensorName, c.ConversionSense, c.ScaleType, c.SignalType, c.ModuleType,
                    Num(c.RawLow), Num(c.RawHigh), Num(c.EuLow), Num(c.EuHigh), Num(c.EuLowSi), Num(c.EuHighSi),
                    signal.IsComplete ? "yes" : "no");

                if (!signal.IsComplete)
                {
                    _preview.Rows[row].DefaultCellStyle.BackColor = Color.MistyRose;
                }
            }
        }

        private void SaveSignalSet()
        {
            if (ResultSet.Count == 0)
            {
                _issues.Items.Add("Build the preview before saving.");
                return;
            }

            string defaultPath = System.IO.Path.Combine(ConfigurationPaths.RootFolder, "last-signal-set.xml");
            using (var save = new SaveFileDialog { Filter = "Signal set (*.xml)|*.xml", FileName = System.IO.Path.GetFileName(defaultPath), InitialDirectory = ConfigurationPaths.RootFolder })
            {
                if (save.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                SignalConfigSetXml.Save(ResultSet.Complete.Select(s => s.Config), save.FileName);
                _status.Text = $"Saved {ResultSet.Complete.Count()} complete signal(s) to {save.FileName}";
            }
        }

        // --- helpers -----------------------------------------------------

        private static string Num(double value) => value == 0 ? "—" : value.ToString("0.######");

        private static Font Bold() => new Font(SystemFonts.DefaultFont, FontStyle.Bold);

        private static Button Button(string text, Action onClick)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (s, e) => onClick();
            return button;
        }
    }
}
