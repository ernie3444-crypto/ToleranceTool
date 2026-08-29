using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Scales;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.UI.Tolerances
{
    /// <summary>
    /// The Tolerance Editor (architecture doc §8): a grid over the tolerance library
    /// with add / delete / modify, inline term editing, live band preview, and a
    /// validation panel.
    /// </summary>
    public sealed class ToleranceEditorForm : Form
    {
        private readonly ScaleCurveLibrary _curves = ScaleCurveLibrary.CreateDefault();
        private readonly ToleranceEngine _engine;

        private ToleranceLibrary _library = new ToleranceLibrary();
        private string _path;

        private SplitContainer _outer = null!;
        private SplitContainer _top = null!;
        private SplitContainer _bottom = null!;

        private readonly ListView _definitions = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            Dock = DockStyle.Fill,
        };

        private readonly ListBox _terms = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly ListBox _issues = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly TextBox _preview = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font(FontFamily.GenericMonospace, 8.5f) };

        private readonly TextBox _rawLow = Num("4");
        private readonly TextBox _rawHigh = Num("20");
        private readonly TextBox _euLow = Num("0");
        private readonly TextBox _euHigh = Num("100");
        private readonly TextBox _euLowSi = Num("0");
        private readonly TextBox _euHighSi = Num("100");
        private readonly TextBox _expected = Num("50");
        private readonly ComboBox _scaleType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        private readonly ComboBox _sense = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        private readonly ComboBox _unitSystem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray };

        public ToleranceEditorForm(string? path = null)
        {
            _engine = new ToleranceEngine(_curves);
            _path = path ?? ConfigurationPaths.ToleranceLibraryFile;

            Text = "Tolerance Editor";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(980, 700);
            MinimumSize = new Size(780, 560);

            _definitions.Columns.Add("Signal Type", 130);
            _definitions.Columns.Add("Module Type", 110);
            _definitions.Columns.Add("Band", 520);
            _definitions.SelectedIndexChanged += (s, e) => OnDefinitionSelected();

            _scaleType.Items.AddRange(new object[] { ScaleTypeNames.Linear, ScaleTypeNames.SquareRoot, ScaleTypeNames.Logarithmic });
            _scaleType.SelectedIndex = 0;
            _sense.Items.AddRange(new object[] { ConversionSense.Direct, ConversionSense.Reverse });
            _sense.SelectedIndex = 0;
            _unitSystem.Items.AddRange(new object[] { UnitSystem.English, UnitSystem.Si });
            _unitSystem.SelectedIndex = 0;
            foreach (Control c in new Control[] { _rawLow, _rawHigh, _euLow, _euHigh, _euLowSi, _euHighSi, _expected })
            {
                ((TextBox)c).TextChanged += (s, e) => RefreshPreview();
            }

            _scaleType.SelectedIndexChanged += (s, e) => RefreshPreview();
            _sense.SelectedIndexChanged += (s, e) => RefreshPreview();
            _unitSystem.SelectedIndexChanged += (s, e) => RefreshPreview();

            Controls.Add(BuildBody());
            Controls.Add(BuildToolbar());
            Controls.Add(_status);

            LoadLibrary(_path, announceMissing: false);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SplitAt(_outer, 0.40);
            SplitAt(_top, 0.52);
            SplitAt(_bottom, 0.60);

            if (Selected == null && _definitions.Items.Count > 0)
            {
                _definitions.Items[0].Selected = true;
                _definitions.Select();
            }

            RefreshPreview();
        }

        private static void SplitAt(SplitContainer split, double fraction)
        {
            try
            {
                int extent = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
                int distance = Math.Max(split.Panel1MinSize, Math.Min((int)(extent * fraction), extent - split.Panel2MinSize));
                if (distance > 0)
                {
                    split.SplitterDistance = distance;
                }
            }
            catch (InvalidOperationException)
            {
                // window too small at load — the default split is fine
            }
        }

        // --- layout -----------------------------------------------------------

        private Control BuildToolbar()
        {
            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            bar.Items.Add(new ToolStripButton("Add", null, (s, e) => AddDefinition()));
            bar.Items.Add(new ToolStripButton("Delete", null, (s, e) => DeleteDefinition()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripButton("Load…", null, (s, e) => LoadFromDialog()));
            bar.Items.Add(new ToolStripButton("Save", null, (s, e) => Save(_path)));
            bar.Items.Add(new ToolStripButton("Save As…", null, (s, e) => SaveAsDialog()));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripButton("Revalidate", null, (s, e) => RefreshValidation()));
            return bar;
        }

        private Control BuildBody()
        {
            var outer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _outer = outer;

            // top: definitions | terms
            var top = new SplitContainer { Dock = DockStyle.Fill };
            _top = top;
            top.Panel1.Controls.Add(_definitions);
            top.Panel1.Controls.Add(new Label { Text = "Definitions", Dock = DockStyle.Top, Height = 20, Font = Bold() });

            var termsPanel = new Panel { Dock = DockStyle.Fill };
            var termButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32 };
            termButtons.Controls.Add(MakeButton("Add term", AddTerm));
            termButtons.Controls.Add(MakeButton("Edit", EditTerm));
            termButtons.Controls.Add(MakeButton("Remove", RemoveTerm));
            termsPanel.Controls.Add(_terms);
            termsPanel.Controls.Add(termButtons);
            termsPanel.Controls.Add(new Label { Text = "Terms of selected definition", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            top.Panel2.Controls.Add(termsPanel);

            // bottom: preview | issues
            var bottom = new SplitContainer { Dock = DockStyle.Fill };
            _bottom = bottom;

            var previewPanel = new Panel { Dock = DockStyle.Fill };
            previewPanel.Controls.Add(_preview);
            previewPanel.Controls.Add(BuildPreviewInputs());
            previewPanel.Controls.Add(new Label { Text = "Live preview  —  sample signal + expected value → resolved band", Dock = DockStyle.Top, Height = 20, Font = Bold() });
            bottom.Panel1.Controls.Add(previewPanel);

            bottom.Panel2.Controls.Add(_issues);
            bottom.Panel2.Controls.Add(new Label { Text = "Validation", Dock = DockStyle.Top, Height = 20, Font = Bold() });

            outer.Panel1.Controls.Add(top);
            outer.Panel2.Controls.Add(bottom);
            return outer;
        }

        private Control BuildPreviewInputs()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 6, AutoSize = true, Padding = new Padding(4) };
            void Pair(string label, Control control)
            {
                grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) });
                grid.Controls.Add(control);
            }

            Pair("Raw low", _rawLow);
            Pair("Raw high", _rawHigh);
            Pair("Scale", _scaleType);
            Pair("EU low", _euLow);
            Pair("EU high", _euHigh);
            Pair("Sense", _sense);
            Pair("EU low (SI)", _euLowSi);
            Pair("EU high (SI)", _euHighSi);
            Pair("Units", _unitSystem);
            Pair("Expected", _expected);

            var recalc = new Button { Text = "Recalculate", AutoSize = true, Margin = new Padding(3) };
            recalc.Click += (s, e) => RefreshPreview();
            grid.Controls.Add(recalc);
            return grid;
        }

        // --- library load / save --------------------------------------------

        private void LoadLibrary(string path, bool announceMissing)
        {
            if (!File.Exists(path))
            {
                if (announceMissing)
                {
                    MessageBox.Show(this, $"No file at {path}.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                _library = new ToleranceLibrary();
                _path = path;
                RefreshAll();
                return;
            }

            ConfigLoadResult<ToleranceLibrary> result = ToleranceLibraryXml.Load(path);
            _library = result.Value;
            _path = path;
            RefreshAll();
            ShowLoadIssues(result.Issues);
        }

        private void LoadFromDialog()
        {
            using (var open = new OpenFileDialog { Filter = "Tolerance library (*.xml)|*.xml|All files (*.*)|*.*" })
            {
                if (File.Exists(_path))
                {
                    open.InitialDirectory = Path.GetDirectoryName(_path);
                    open.FileName = Path.GetFileName(_path);
                }

                if (open.ShowDialog(this) == DialogResult.OK)
                {
                    LoadLibrary(open.FileName, announceMissing: true);
                }
            }
        }

        private void Save(string path)
        {
            try
            {
                ToleranceLibraryXml.Save(_library, path);
                _path = path;
                _status.Text = $"Saved {DateTime.Now:t} — {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveAsDialog()
        {
            using (var save = new SaveFileDialog { Filter = "Tolerance library (*.xml)|*.xml", FileName = Path.GetFileName(_path) })
            {
                if (File.Exists(_path))
                {
                    save.InitialDirectory = Path.GetDirectoryName(_path);
                }

                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    Save(save.FileName);
                }
            }
        }

        private void ShowLoadIssues(IReadOnlyList<ConfigIssue> issues)
        {
            if (issues.Count == 0)
            {
                _status.Text = $"Loaded {_library.Count} definition(s) — {_path}";
                return;
            }

            _status.Text = $"Loaded with {issues.Count} issue(s) — {_path}";
        }

        // --- definitions ----------------------------------------------------

        private ToleranceDefinition? Selected =>
            _definitions.SelectedItems.Count > 0
                ? _definitions.SelectedItems[0].Tag as ToleranceDefinition
                : null;

        private void AddDefinition()
        {
            using (var dialog = new AddDefinitionDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var definition = new ToleranceDefinition { SignalType = dialog.SignalType, ModuleType = dialog.ModuleType };
                    _library.Add(definition);
                    RefreshDefinitions();
                    SelectDefinition(definition);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void DeleteDefinition()
        {
            ToleranceDefinition? definition = Selected;
            if (definition == null)
            {
                return;
            }

            if (MessageBox.Show(this, $"Delete the tolerance for {ToleranceLibrary.KeyOf(definition)}?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _library.Remove(definition);
                RefreshAll();
            }
        }

        // --- terms --------------------------------------------------------

        private void AddTerm()
        {
            ToleranceDefinition? definition = Selected;
            if (definition == null)
            {
                return;
            }

            using (var dialog = new ToleranceTermDialog(null))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    definition.Terms.Add(dialog.Result);
                    RefreshAfterTermChange(definition);
                }
            }
        }

        private void EditTerm()
        {
            ToleranceDefinition? definition = Selected;
            int index = _terms.SelectedIndex;
            if (definition == null || index < 0 || index >= definition.Terms.Count)
            {
                return;
            }

            using (var dialog = new ToleranceTermDialog(definition.Terms[index]))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    definition.Terms[index] = dialog.Result;
                    RefreshAfterTermChange(definition);
                }
            }
        }

        private void RemoveTerm()
        {
            ToleranceDefinition? definition = Selected;
            int index = _terms.SelectedIndex;
            if (definition == null || index < 0 || index >= definition.Terms.Count)
            {
                return;
            }

            definition.Terms.RemoveAt(index);
            RefreshAfterTermChange(definition);
        }

        private void RefreshAfterTermChange(ToleranceDefinition definition)
        {
            RefreshTerms();
            UpdateDefinitionRow(definition);
            RefreshValidation();
            RefreshPreview();
        }

        // --- refresh ------------------------------------------------------

        private void RefreshAll()
        {
            RefreshDefinitions();
            RefreshValidation();
        }

        private void RefreshDefinitions()
        {
            _definitions.BeginUpdate();
            _definitions.Items.Clear();
            foreach (ToleranceDefinition definition in _library.Definitions)
            {
                var item = new ListViewItem(definition.SignalType) { Tag = definition };
                item.SubItems.Add(definition.ModuleType);
                item.SubItems.Add(ToleranceTermText.DescribeDefinition(definition));
                _definitions.Items.Add(item);
            }

            _definitions.EndUpdate();
            OnDefinitionSelected();
        }

        private void UpdateDefinitionRow(ToleranceDefinition definition)
        {
            foreach (ListViewItem item in _definitions.Items)
            {
                if (ReferenceEquals(item.Tag, definition))
                {
                    item.SubItems[0].Text = definition.SignalType;
                    item.SubItems[1].Text = definition.ModuleType;
                    item.SubItems[2].Text = ToleranceTermText.DescribeDefinition(definition);
                    return;
                }
            }
        }

        private void OnDefinitionSelected()
        {
            RefreshTerms();
            RefreshPreview();
        }

        private void RefreshTerms()
        {
            _terms.BeginUpdate();
            _terms.Items.Clear();
            ToleranceDefinition? definition = Selected;
            if (definition != null)
            {
                foreach (ToleranceTerm term in definition.Terms)
                {
                    _terms.Items.Add(ToleranceTermText.Describe(term));
                }
            }

            _terms.EndUpdate();
        }

        private void RefreshValidation()
        {
            _issues.BeginUpdate();
            _issues.Items.Clear();

            foreach (string key in _library.DuplicateKeys())
            {
                _issues.Items.Add($"Error [{key}]: defined more than once.");
            }

            foreach (ToleranceDefinition definition in _library.Definitions)
            {
                if (definition.Terms.Count == 0)
                {
                    _issues.Items.Add($"Error [{ToleranceLibrary.KeyOf(definition)}]: no terms.");
                }
            }

            foreach (ConfigIssue issue in ToleranceLibraryValidator.Validate(_library))
            {
                _issues.Items.Add(issue.ToString());
            }

            if (_issues.Items.Count == 0)
            {
                _issues.Items.Add("No problems found.");
            }

            _issues.EndUpdate();
        }

        private void RefreshPreview()
        {
            try
            {
                ToleranceDefinition? definition = Selected;
                if (definition == null)
                {
                    _preview.Text = _library.Count == 0
                        ? "No tolerances yet — add one, then this shows its resolved band."
                        : "Select a definition on the left to preview its band.";
                    return;
                }

                if (definition.Terms.Count == 0)
                {
                    _preview.Text = $"\"{ToleranceLibrary.KeyOf(definition)}\" has no terms yet. Add a term to see the band.";
                    return;
                }

                if (!TryReadSignal(out SignalConfig signal, out string error))
                {
                    _preview.Text = "Preview inputs: " + error;
                    return;
                }

                if (!double.TryParse(_expected.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double expected))
                {
                    _preview.Text = "Preview inputs: the expected value must be a number.";
                    return;
                }

                var unitSystem = _unitSystem.SelectedItem is UnitSystem u ? u : UnitSystem.English;
                ToleranceResult result = _engine.Calculate(expected, unitSystem, signal, definition);
                _preview.Text = Describe(result, expected, unitSystem);
            }
            catch (Exception ex)
            {
                _preview.Text = "Preview error: " + ex.Message;
            }
        }

        private bool TryReadSignal(out SignalConfig signal, out string error)
        {
            var s = new SignalConfig();
            signal = s;

            var fields = new (TextBox Box, string Name, Action<double> Set)[]
            {
                (_rawLow, "raw low", v => s.RawLow = v),
                (_rawHigh, "raw high", v => s.RawHigh = v),
                (_euLow, "EU low", v => s.EuLow = v),
                (_euHigh, "EU high", v => s.EuHigh = v),
                (_euLowSi, "EU low (SI)", v => s.EuLowSi = v),
                (_euHighSi, "EU high (SI)", v => s.EuHighSi = v),
            };

            foreach ((TextBox box, string name, Action<double> set) in fields)
            {
                if (!double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    error = $"{name} must be a number.";
                    return false;
                }

                set(value);
            }

            ToleranceDefinition? definition = Selected;
            if (definition != null && !definition.IsEuOnly && s.RawLow == s.RawHigh)
            {
                error = "raw low and raw high must differ — this band applies in raw units and needs a raw range.";
                return false;
            }

            s.SignalType = definition?.SignalType ?? string.Empty;
            s.ModuleType = definition?.ModuleType ?? string.Empty;
            s.ScaleType = _scaleType.SelectedItem as string ?? ScaleTypeNames.Linear;
            s.ConversionSense = _sense.SelectedItem is ConversionSense sense ? sense : ConversionSense.Direct;
            error = string.Empty;
            return true;
        }

        private static string Describe(ToleranceResult result, double expected, UnitSystem unitSystem)
        {
            var sb = new StringBuilder();

            if (result.Outcome != ToleranceOutcome.Calculated)
            {
                sb.AppendLine($"Not calculated: {result.Outcome}");
                sb.AppendLine(result.Message);
                if (result.RawExpected != 0)
                {
                    sb.AppendLine($"rawExpected = {result.RawExpected:0.######}");
                }

                return sb.ToString();
            }

            sb.AppendLine(result.UsedEuFastPath ? "Path A — EU fast path" : "Path B — raw round-trip");
            sb.AppendLine();
            foreach (ResolvedTerm term in result.Terms)
            {
                string space = term.Space == ToleranceSpace.Eu ? "EU " : "raw";
                sb.AppendLine($"  {space}  {term.Magnitude,14:0.########}   {Configuration.Tolerances.ToleranceTermText.Describe(term.Source)}");
            }

            sb.AppendLine();
            if (!result.UsedEuFastPath)
            {
                sb.AppendLine($"  rawExpected     {result.RawExpected,14:0.########}");
                sb.AppendLine($"  raw band ±      {result.RawTolerance,14:0.########}");
                sb.AppendLine($"  EU +            {result.EuPlus,14:0.########}");
                sb.AppendLine($"  EU -            {result.EuMinus,14:0.########}");
                if (result.Extrapolated)
                {
                    sb.AppendLine("  (extrapolated past the raw range)");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"  tolerance ±  =  {result.Tolerance:0.########}  ({unitSystem} EU)");
            sb.AppendLine($"  {expected - result.Tolerance:0.######}  …  {expected + result.Tolerance:0.######}");
            return sb.ToString();
        }

        private void SelectDefinition(ToleranceDefinition definition)
        {
            foreach (ListViewItem item in _definitions.Items)
            {
                if (ReferenceEquals(item.Tag, definition))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    return;
                }
            }
        }

        // --- small helpers -----------------------------------------------

        private static TextBox Num(string value) => new TextBox { Text = value, Width = 70 };

        private static Font Bold() => new Font(SystemFonts.DefaultFont, FontStyle.Bold);

        private static Button MakeButton(string text, Action onClick)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (s, e) => onClick();
            return button;
        }
    }
}
