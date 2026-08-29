using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Aliases;

namespace ToleranceTool.UI.Datasheet
{
    /// <summary>Editor over <c>alias-tables.xml</c>: named, prioritized System ID → signal rules.</summary>
    public sealed class AliasTableEditorForm : Form
    {
        private AliasTableSet _set = AliasTableSet.Empty();
        private string _path;

        private readonly ListBox _tables = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly TextBox _name = new TextBox { Dock = DockStyle.Top };
        private readonly TextBox _priority = new TextBox { Dock = DockStyle.Top, Text = "0" };
        private readonly DataGridView _entries = new DataGridView
        {
            Dock = DockStyle.Fill,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = true,
        };

        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
        private bool _loading;

        public AliasTableEditorForm(string? path = null)
        {
            _path = path ?? ConfigurationPaths.AliasTablesFile;

            Text = "Alias Table Editor";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 480);

            _entries.Columns.Add(new DataGridViewTextBoxColumn { Name = "SystemId", HeaderText = "System ID / pattern" });
            _entries.Columns.Add(new DataGridViewComboBoxColumn { Name = "TargetKind", HeaderText = "Target", Items = { "SensorName", "UniversalId" } });
            _entries.Columns.Add(new DataGridViewTextBoxColumn { Name = "Target", HeaderText = "Target value" });
            _entries.Columns.Add(new DataGridViewComboBoxColumn { Name = "Match", HeaderText = "Match", Items = { "exact", "contains", "regex" } });

            _tables.SelectedIndexChanged += (s, e) => LoadSelected();
            _name.TextChanged += (s, e) => WriteBack();
            _priority.TextChanged += (s, e) => WriteBack();
            _entries.CellEndEdit += (s, e) => WriteBack();
            _entries.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_entries.IsCurrentCellDirty)
                {
                    _entries.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            bar.Items.Add(new ToolStripButton("Add table", null, (s, e) => AddTable()));
            bar.Items.Add(new ToolStripButton("Delete table", null, (s, e) => DeleteTable()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripButton("Load…", null, (s, e) => LoadFromDialog()));
            bar.Items.Add(new ToolStripButton("Save", null, (s, e) => Save(_path)));
            bar.Items.Add(new ToolStripButton("Save As…", null, (s, e) => SaveAsDialog()));

            Controls.Add(BuildBody());
            Controls.Add(bar);
            Controls.Add(_status);

            if (File.Exists(_path))
            {
                LoadFrom(_path);
            }
        }

        private Control BuildBody()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 200 };
            split.Panel1.Controls.Add(_tables);
            split.Panel1.Controls.Add(new Label { Text = "Alias tables", Dock = DockStyle.Top, Height = 20, Font = Bold() });

            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(_entries);
            var header = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, Height = 32 };
            header.Controls.Add(new Label { Text = "Name", AutoSize = true, Anchor = AnchorStyles.Left });
            header.Controls.Add(_name);
            header.Controls.Add(new Label { Text = "Priority", AutoSize = true, Anchor = AnchorStyles.Left });
            header.Controls.Add(_priority);
            right.Controls.Add(header);
            split.Panel2.Controls.Add(right);
            return split;
        }

        private void LoadFrom(string path)
        {
            ConfigLoadResult<AliasTableSet> result = AliasTablesXml.Load(path);
            _set = result.Value;
            _path = path;
            RefreshTables();
            _status.Text = result.Issues.Count == 0
                ? $"Loaded {_set.Tables.Count} table(s) — {path}"
                : $"Loaded with {result.Issues.Count} issue(s): {result.Issues[0].Message}";
        }

        private void LoadFromDialog()
        {
            using (var open = new OpenFileDialog { Filter = "Alias tables (*.xml)|*.xml|All files (*.*)|*.*" })
            {
                if (open.ShowDialog(this) == DialogResult.OK)
                {
                    LoadFrom(open.FileName);
                }
            }
        }

        private void Save(string path)
        {
            try
            {
                AliasTablesXml.Save(_set, path);
                _path = path;
                _status.Text = $"Saved {_set.Tables.Count} table(s) — {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveAsDialog()
        {
            using (var save = new SaveFileDialog { Filter = "Alias tables (*.xml)|*.xml", FileName = Path.GetFileName(_path) })
            {
                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    Save(save.FileName);
                }
            }
        }

        private AliasTable? Selected =>
            _tables.SelectedIndex >= 0 && _tables.SelectedIndex < _set.Tables.Count ? _set.Tables[_tables.SelectedIndex] : null;

        private void RefreshTables()
        {
            int selected = _tables.SelectedIndex;
            _tables.BeginUpdate();
            _tables.Items.Clear();
            foreach (AliasTable table in _set.Tables)
            {
                _tables.Items.Add($"{table.Name}  (priority {table.Priority}, {table.Entries.Count})");
            }

            _tables.EndUpdate();
            if (selected >= 0 && selected < _tables.Items.Count)
            {
                _tables.SelectedIndex = selected;
            }
            else if (_tables.Items.Count > 0)
            {
                _tables.SelectedIndex = 0;
            }
            else
            {
                LoadSelected();
            }
        }

        private void AddTable()
        {
            _set.Add(new AliasTable { Name = "New table", Priority = (_set.Tables.Count + 1) * 10 });
            RefreshTables();
            _tables.SelectedIndex = _set.Tables.Count - 1;
        }

        private void DeleteTable()
        {
            AliasTable? table = Selected;
            if (table != null)
            {
                var remaining = _set.Tables.Where(t => !ReferenceEquals(t, table)).ToList();
                _set = AliasTableSet.Empty();
                remaining.ForEach(_set.Add);
                RefreshTables();
            }
        }

        private void LoadSelected()
        {
            _loading = true;
            AliasTable? table = Selected;
            bool enabled = table != null;
            _name.Enabled = _priority.Enabled = _entries.Enabled = enabled;

            _name.Text = table?.Name ?? string.Empty;
            _priority.Text = table?.Priority.ToString() ?? "0";
            _entries.Rows.Clear();
            if (table != null)
            {
                foreach (AliasEntry entry in table.Entries)
                {
                    _entries.Rows.Add(
                        entry.SystemId,
                        entry.UniversalId != null ? "UniversalId" : "SensorName",
                        entry.UniversalId ?? entry.SensorName ?? string.Empty,
                        entry.Match.ToString().ToLowerInvariant());
                }
            }

            _loading = false;
        }

        private void WriteBack()
        {
            if (_loading || Selected == null)
            {
                return;
            }

            AliasTable table = Selected;
            table.Name = _name.Text.Trim();
            table.Priority = int.TryParse(_priority.Text.Trim(), out int p) ? p : 0;

            table.Entries.Clear();
            foreach (DataGridViewRow row in _entries.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                string systemId = Convert.ToString(row.Cells["SystemId"].Value)?.Trim() ?? string.Empty;
                string target = Convert.ToString(row.Cells["Target"].Value)?.Trim() ?? string.Empty;
                if (systemId.Length == 0 || target.Length == 0)
                {
                    continue;
                }

                bool universal = string.Equals(Convert.ToString(row.Cells["TargetKind"].Value), "UniversalId", StringComparison.OrdinalIgnoreCase);
                Enum.TryParse(Convert.ToString(row.Cells["Match"].Value) ?? "exact", true, out AliasMatch match);

                table.Entries.Add(new AliasEntry
                {
                    SystemId = systemId,
                    SensorName = universal ? null : target,
                    UniversalId = universal ? target : null,
                    Match = match,
                });
            }

            int index = _tables.SelectedIndex;
            if (index >= 0)
            {
                _tables.Items[index] = $"{table.Name}  (priority {table.Priority}, {table.Entries.Count})";
            }
        }

        private static Font Bold() => new Font(SystemFonts.DefaultFont, FontStyle.Bold);
    }
}
