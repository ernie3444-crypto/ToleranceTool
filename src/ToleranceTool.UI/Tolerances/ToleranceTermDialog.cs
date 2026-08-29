using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using ToleranceTool.Configuration;
using ToleranceTool.Configuration.Tolerances;
using ToleranceTool.Core.Signals;
using ToleranceTool.Core.Tolerances;

namespace ToleranceTool.UI.Tolerances
{
    /// <summary>Add or edit a single tolerance term.</summary>
    public sealed class ToleranceTermDialog : Form
    {
        private readonly ComboBox _kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _value = new TextBox();
        private readonly ComboBox _basis = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _space = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _unit = new TextBox();
        private readonly ComboBox _unitSystem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _expression = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 60 };
        private readonly Label _hint = new Label { ForeColor = Color.DimGray, AutoSize = false };
        private readonly TableLayoutPanel _rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };

        public ToleranceTermDialog(ToleranceTerm? existing)
        {
            Text = existing == null ? "Add tolerance term" : "Edit tolerance term";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(420, 320);

            _kind.Items.AddRange(new object[]
            {
                ToleranceTermKind.Percent,
                ToleranceTermKind.AbsoluteEu,
                ToleranceTermKind.AbsoluteRaw,
                ToleranceTermKind.Expression,
            });
            _basis.Items.AddRange(new object[] { PercentBasis.RawSpan, PercentBasis.EuSpan, PercentBasis.Reading });
            _space.Items.AddRange(new object[] { ToleranceSpace.Raw, ToleranceSpace.Eu });
            _unitSystem.Items.AddRange(new object[] { UnitSystem.English, UnitSystem.Si });

            _kind.SelectedIndexChanged += (s, e) => SyncVisibility();

            _rows.Padding = new Padding(12);
            AddRow("Kind", _kind);
            AddRow("Value", _value);
            AddRow("Basis", _basis);
            AddRow("Space", _space);
            AddRow("Unit", _unit);
            AddRow("Unit system", _unitSystem);
            AddRow("Expression", _expression);
            _hint.Height = 34;
            _rows.Controls.Add(_hint, 0, _rows.RowCount);
            _rows.SetColumnSpan(_hint, 2);
            _rows.RowCount++;

            var ok = new Button { Text = "OK", DialogResult = DialogResult.None, Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            ok.Click += (s, e) => TryAccept();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(8) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(_rows);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            LoadFrom(existing ?? new ToleranceTerm { Kind = ToleranceTermKind.Percent, Value = 0.003, PercentBasis = PercentBasis.RawSpan });
            SyncVisibility();
        }

        public ToleranceTerm Result { get; private set; } = new ToleranceTerm();

        private void AddRow(string label, Control control)
        {
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Width = 250;
            var caption = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) };
            _rows.Controls.Add(caption, 0, _rows.RowCount);
            _rows.Controls.Add(control, 1, _rows.RowCount);
            _rows.RowCount++;
        }

        private void LoadFrom(ToleranceTerm term)
        {
            _kind.SelectedItem = term.Kind;
            _value.Text = term.Value.ToString("R", CultureInfo.InvariantCulture);
            _basis.SelectedItem = term.PercentBasis;
            _space.SelectedItem = term.Space;
            _unit.Text = term.Unit;
            _unitSystem.SelectedItem = term.UnitSystem;
            _expression.Text = term.ExpressionBody;
        }

        private void SyncVisibility()
        {
            var kind = (ToleranceTermKind)_kind.SelectedItem;
            SetRowVisible(_value, kind != ToleranceTermKind.Expression);
            SetRowVisible(_basis, kind == ToleranceTermKind.Percent);
            SetRowVisible(_space, kind == ToleranceTermKind.Percent || kind == ToleranceTermKind.Expression);
            SetRowVisible(_unit, kind == ToleranceTermKind.AbsoluteEu || kind == ToleranceTermKind.AbsoluteRaw);
            SetRowVisible(_unitSystem, kind == ToleranceTermKind.AbsoluteEu);
            SetRowVisible(_expression, kind == ToleranceTermKind.Expression);

            _hint.Text = kind == ToleranceTermKind.Expression
                ? "Variables: " + string.Join(", ", ToleranceExpressionVariables.All)
                : kind == ToleranceTermKind.Percent
                    ? "Value is a fraction: 0.3% is 0.003."
                    : string.Empty;
        }

        private void SetRowVisible(Control control, bool visible)
        {
            control.Visible = visible;
            int row = _rows.GetRow(control);
            if (_rows.GetControlFromPosition(0, row) is Control caption)
            {
                caption.Visible = visible;
            }
        }

        private void TryAccept()
        {
            var kind = (ToleranceTermKind)_kind.SelectedItem;
            var term = new ToleranceTerm { Kind = kind };

            if (kind != ToleranceTermKind.Expression)
            {
                if (!double.TryParse(_value.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    Warn("Value must be a number.");
                    return;
                }

                term.Value = value;
            }

            switch (kind)
            {
                case ToleranceTermKind.Percent:
                    term.PercentBasis = (PercentBasis)_basis.SelectedItem;
                    term.Space = (ToleranceSpace)_space.SelectedItem;
                    break;

                case ToleranceTermKind.AbsoluteEu:
                    term.Unit = _unit.Text.Trim();
                    term.UnitSystem = (UnitSystem)_unitSystem.SelectedItem;
                    break;

                case ToleranceTermKind.AbsoluteRaw:
                    term.Unit = _unit.Text.Trim();
                    break;

                case ToleranceTermKind.Expression:
                    term.ExpressionBody = _expression.Text.Trim();
                    term.Space = (ToleranceSpace)_space.SelectedItem;
                    break;
            }

            List<ConfigIssue> issues = ToleranceLibraryValidator.ValidateTerm(term)
                .Where(i => i.Severity == ConfigSeverity.Error)
                .ToList();
            if (issues.Count > 0)
            {
                Warn(string.Join(Environment.NewLine, issues.Select(i => i.Message)));
                return;
            }

            Result = term;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string message) =>
            MessageBox.Show(this, message, "Tolerance term", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
