using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Scales;
using ToleranceTool.Core.Scales;

namespace ToleranceTool.UI.Scales
{
    /// <summary>
    /// Editor over <c>scale-types.xml</c>: forward + inverse expressions per curve,
    /// with the endpoint/monotonic validator and a live plot.
    /// </summary>
    public sealed class ScaleTypeEditorForm : Form
    {
        private readonly List<ScaleType> _scaleTypes = new List<ScaleType>();
        private string _path;

        private readonly ListBox _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly TextBox _name = new TextBox { Dock = DockStyle.Top };
        private readonly TextBox _forward = new TextBox { Dock = DockStyle.Top };
        private readonly TextBox _inverse = new TextBox { Dock = DockStyle.Top };
        private readonly DataGridView _params = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = 96,
            RowHeadersVisible = false,
            AllowUserToAddRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = System.Drawing.SystemColors.Window,
            BorderStyle = BorderStyle.Fixed3D,
        };

        private readonly CurvePlot _plot = new CurvePlot { Dock = DockStyle.Fill };
        private readonly ListBox _issues = new ListBox { Dock = DockStyle.Bottom, Height = 80, IntegralHeight = false };
        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };

        private SplitContainer _outer = null!;
        private SplitContainer _right = null!;
        private bool _loading;

        public ScaleTypeEditorForm(string? path = null)
        {
            _path = path ?? ConfigurationPaths.ScaleTypeLibraryFile;

            Text = "Scale Type Editor";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(960, 620);
            MinimumSize = new Size(720, 500);

            _params.Columns.Add(new DataGridViewTextBoxColumn { Name = "Param", HeaderText = "Parameter" });
            _params.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Value" });

            _list.SelectedIndexChanged += (s, e) => LoadSelected();
            _name.TextChanged += (s, e) => WriteBack();
            _forward.TextChanged += (s, e) => WriteBackAndValidate();
            _inverse.TextChanged += (s, e) => WriteBackAndValidate();
            _params.CellEndEdit += (s, e) => WriteBackAndValidate();

            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            bar.Items.Add(new ToolStripButton("Add", null, (s, e) => AddScaleType()));
            bar.Items.Add(new ToolStripButton("Delete", null, (s, e) => DeleteScaleType()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripButton("Load…", null, (s, e) => LoadFromDialog()));
            bar.Items.Add(new ToolStripButton("Save", null, (s, e) => Save(_path)));
            bar.Items.Add(new ToolStripButton("Save As…", null, (s, e) => SaveAsDialog()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripButton("Validate", null, (s, e) => ValidateCurrent()));

            Controls.Add(BuildBody());
            Controls.Add(bar);
            Controls.Add(_issues);
            Controls.Add(_status);

            if (File.Exists(_path))
            {
                LoadFrom(_path);
            }
            else
            {
                SeedDefaults();
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FormLayout.SetSplit(_outer, 0.24, 140, 220);
            FormLayout.SetSplit(_right, 0.52, 170, 120);
        }

        private Control BuildBody()
        {
            _outer = new SplitContainer { Dock = DockStyle.Fill };
            _outer.Panel1.Controls.Add(_list);
            _outer.Panel1.Controls.Add(new Label { Text = "Scale types", Dock = DockStyle.Top, Height = 20, Font = Bold() });

            _right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

            var editor = new Panel { Dock = DockStyle.Fill };
            editor.Controls.Add(_params);                                                  // fills the space below the fields
            editor.Controls.Add(new Label { Text = "Parameters", Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray, Padding = new Padding(2, 4, 0, 0) });
            editor.Controls.Add(Labeled("Inverse   rawFrac → euFrac", _inverse));
            editor.Controls.Add(Labeled("Forward   euFrac → rawFrac", _forward));
            editor.Controls.Add(Labeled("Name", _name));
            _params.Dock = DockStyle.Fill;
            _params.Height = 0;
            _right.Panel1.Controls.Add(editor);

            _right.Panel2.Controls.Add(_plot);
            _right.Panel2.Controls.Add(new Label { Text = "Curve   (blue = Forward, green = Inverse, grey = y=x)", Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray });

            _outer.Panel2.Controls.Add(_right);
            return _outer;
        }

        private static Control Labeled(string caption, Control control)
        {
            var host = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(2, 2, 2, 4) };
            control.Dock = DockStyle.Fill;
            var label = new Label { Text = caption, Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray };
            host.Controls.Add(control);
            host.Controls.Add(label);
            return host;
        }

        // --- data -------------------------------------------------------

        private void SeedDefaults()
        {
            _scaleTypes.Clear();
            _scaleTypes.Add(new ScaleType { Name = "Linear", Forward = "x", Inverse = "x" });
            _scaleTypes.Add(new ScaleType { Name = "SquareRoot", Forward = "Pow(x, 2)", Inverse = "Sqrt(x)" });
            var log = new ScaleType
            {
                Name = "Logarithmic",
                Forward = "(Pow(10, x * decades) - 1) / (Pow(10, decades) - 1)",
                Inverse = "Log10(x * (Pow(10, decades) - 1) + 1) / decades",
            };
            log.Parameters["decades"] = 2;
            _scaleTypes.Add(log);
            RefreshList();
            _status.Text = "Seeded with the built-in curves. Save to create the library file.";
        }

        private void LoadFrom(string path)
        {
            ConfigLoadResult<List<ScaleType>> result = ScaleTypeLibraryXml.Load(path);
            _scaleTypes.Clear();
            _scaleTypes.AddRange(result.Value);
            _path = path;
            RefreshList();

            _issues.Items.Clear();
            foreach (ConfigIssue issue in result.Issues)
            {
                _issues.Items.Add(issue.ToString());
            }

            _status.Text = $"Loaded {_scaleTypes.Count} scale type(s) — {path}";
        }

        private void LoadFromDialog()
        {
            using (var open = new OpenFileDialog { Filter = "Scale types (*.xml)|*.xml|All files (*.*)|*.*" })
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
                ScaleTypeLibraryXml.Save(_scaleTypes, path);
                _path = path;
                _status.Text = $"Saved {_scaleTypes.Count} scale type(s) — {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveAsDialog()
        {
            using (var save = new SaveFileDialog { Filter = "Scale types (*.xml)|*.xml", FileName = Path.GetFileName(_path) })
            {
                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    Save(save.FileName);
                }
            }
        }

        // --- list ------------------------------------------------------

        private ScaleType? Selected =>
            _list.SelectedIndex >= 0 && _list.SelectedIndex < _scaleTypes.Count ? _scaleTypes[_list.SelectedIndex] : null;

        private void RefreshList()
        {
            int selected = _list.SelectedIndex;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (ScaleType scaleType in _scaleTypes)
            {
                _list.Items.Add(scaleType.Name);
            }

            _list.EndUpdate();
            if (selected >= 0 && selected < _list.Items.Count)
            {
                _list.SelectedIndex = selected;
            }
            else if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
            else
            {
                LoadSelected();
            }
        }

        private void AddScaleType()
        {
            var scaleType = new ScaleType { Name = "NewCurve", Forward = "x", Inverse = "x" };
            _scaleTypes.Add(scaleType);
            RefreshList();
            _list.SelectedIndex = _scaleTypes.Count - 1;
        }

        private void DeleteScaleType()
        {
            if (Selected != null)
            {
                _scaleTypes.Remove(Selected);
                RefreshList();
            }
        }

        private void LoadSelected()
        {
            _loading = true;
            ScaleType? scaleType = Selected;
            bool enabled = scaleType != null;
            _name.Enabled = _forward.Enabled = _inverse.Enabled = _params.Enabled = enabled;

            _name.Text = scaleType?.Name ?? string.Empty;
            _forward.Text = scaleType?.Forward ?? string.Empty;
            _inverse.Text = scaleType?.Inverse ?? string.Empty;

            _params.Rows.Clear();
            if (scaleType != null)
            {
                foreach (KeyValuePair<string, double> parameter in scaleType.Parameters)
                {
                    _params.Rows.Add(parameter.Key, parameter.Value.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            _loading = false;
            ValidateCurrent();
        }

        private void WriteBack()
        {
            if (_loading || Selected == null)
            {
                return;
            }

            Selected.Name = _name.Text.Trim();
            int index = _list.SelectedIndex;
            if (index >= 0)
            {
                _list.Items[index] = Selected.Name;
            }
        }

        private void WriteBackAndValidate()
        {
            if (_loading || Selected == null)
            {
                return;
            }

            Selected.Forward = _forward.Text.Trim();
            Selected.Inverse = _inverse.Text.Trim();
            Selected.Parameters.Clear();
            foreach (DataGridViewRow row in _params.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                string key = Convert.ToString(row.Cells["Param"].Value)?.Trim() ?? string.Empty;
                if (key.Length > 0 &&
                    double.TryParse(Convert.ToString(row.Cells["Value"].Value)?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    Selected.Parameters[key] = value;
                }
            }

            ValidateCurrent();
        }

        private void ValidateCurrent()
        {
            _issues.Items.Clear();
            ScaleType? scaleType = Selected;
            if (scaleType == null)
            {
                _plot.Show(null);
                return;
            }

            IReadOnlyList<ConfigIssue> issues = ScaleTypeLibraryXml.ValidateCurve(scaleType);
            if (issues.Count == 0)
            {
                _issues.Items.Add($"\"{scaleType.Name}\" satisfies the contract (endpoints + monotonic).");
                _plot.Show(new ScaleCurve(scaleType));
            }
            else
            {
                foreach (ConfigIssue issue in issues)
                {
                    _issues.Items.Add(issue.Message);
                }

                _plot.Show(SafeCurve(scaleType));
            }
        }

        private static ScaleCurve? SafeCurve(ScaleType scaleType)
        {
            try
            {
                return new ScaleCurve(scaleType);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Font Bold() => new Font(SystemFonts.DefaultFont, FontStyle.Bold);
    }
}
