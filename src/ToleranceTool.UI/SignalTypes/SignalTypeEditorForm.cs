using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.SignalTypes;
using ToleranceTool.Core.Signals;

namespace ToleranceTool.UI.SignalTypes
{
    /// <summary>Grid editor over <c>signal-types.xml</c>: name → raw range + unit.</summary>
    public sealed class SignalTypeEditorForm : Form
    {
        private readonly DataGridView _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = System.Drawing.SystemColors.Window,
            BorderStyle = BorderStyle.Fixed3D,
        };

        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
        private string _path;

        public SignalTypeEditorForm(string? path = null)
        {
            _path = path ?? ConfigurationPaths.SignalTypeRegistryFile;

            Text = "Signal Type Registry";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 420);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RawLow", HeaderText = "Raw low" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RawHigh", HeaderText = "Raw high" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Unit" });

            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            bar.Items.Add(new ToolStripButton("Load…", null, (s, e) => LoadFromDialog()));
            bar.Items.Add(new ToolStripButton("Save", null, (s, e) => Save(_path)));
            bar.Items.Add(new ToolStripButton("Save As…", null, (s, e) => SaveAsDialog()));

            Controls.Add(_grid);
            Controls.Add(bar);
            Controls.Add(_status);

            if (File.Exists(_path))
            {
                LoadFrom(_path);
            }
        }

        private void LoadFrom(string path)
        {
            ConfigLoadResult<SignalTypeRegistry> result = SignalTypeRegistryXml.Load(path);
            _grid.Rows.Clear();
            foreach (SignalTypeSpec spec in result.Value.Specs)
            {
                _grid.Rows.Add(spec.Name, Fmt(spec.RawLow), Fmt(spec.RawHigh), spec.Unit);
            }

            _path = path;
            _status.Text = result.Issues.Count == 0
                ? $"Loaded {result.Value.Count} signal type(s) — {path}"
                : $"Loaded with {result.Issues.Count} issue(s): {result.Issues[0].Message}";
        }

        private void LoadFromDialog()
        {
            using (var open = new OpenFileDialog { Filter = "Signal types (*.xml)|*.xml|All files (*.*)|*.*" })
            {
                if (open.ShowDialog(this) == DialogResult.OK)
                {
                    LoadFrom(open.FileName);
                }
            }
        }

        private bool TryReadGrid(out SignalTypeRegistry registry, out string error)
        {
            registry = new SignalTypeRegistry();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                string name = Convert.ToString(row.Cells["Name"].Value)?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    continue;
                }

                if (!TryDouble(row, "RawLow", out double low) || !TryDouble(row, "RawHigh", out double high))
                {
                    error = $"\"{name}\": raw low/high must be numbers.";
                    return false;
                }

                try
                {
                    registry.Add(new SignalTypeSpec
                    {
                        Name = name,
                        RawLow = low,
                        RawHigh = high,
                        Unit = Convert.ToString(row.Cells["Unit"].Value)?.Trim() ?? string.Empty,
                    });
                }
                catch (InvalidOperationException ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void Save(string path)
        {
            if (!TryReadGrid(out SignalTypeRegistry registry, out string error))
            {
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SignalTypeRegistryXml.Save(registry, path);
                _path = path;
                _status.Text = $"Saved {registry.Count} signal type(s) — {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveAsDialog()
        {
            using (var save = new SaveFileDialog { Filter = "Signal types (*.xml)|*.xml", FileName = Path.GetFileName(_path) })
            {
                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    Save(save.FileName);
                }
            }
        }

        private static bool TryDouble(DataGridViewRow row, string column, out double value) =>
            double.TryParse(Convert.ToString(row.Cells[column].Value)?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static string Fmt(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
