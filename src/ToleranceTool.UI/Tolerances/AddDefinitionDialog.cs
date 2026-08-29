using System;
using System.Drawing;
using System.Windows.Forms;

namespace ToleranceTool.UI.Tolerances
{
    /// <summary>Prompts for the signal type + module type of a new tolerance definition.</summary>
    public sealed class AddDefinitionDialog : Form
    {
        private readonly TextBox _signalType = new TextBox { Width = 220, Anchor = AnchorStyles.Left };
        private readonly TextBox _moduleType = new TextBox { Width = 220, Anchor = AnchorStyles.Left };

        public AddDefinitionDialog()
        {
            Text = "New tolerance";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(360, 150);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(14, 14, 14, 4),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = "Signal type", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 12, 3) }, 0, 0);
            layout.Controls.Add(_signalType, 1, 0);
            layout.Controls.Add(new Label { Text = "Module type", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 12, 3) }, 0, 1);
            layout.Controls.Add(_moduleType, 1, 1);

            var ok = new Button { Text = "OK", Width = 84, Margin = new Padding(4) };
            var cancel = new Button { Text = "Cancel", Width = 84, DialogResult = DialogResult.Cancel, Margin = new Padding(4) };
            ok.Click += (s, e) =>
            {
                if (SignalType.Length == 0 || ModuleType.Length == 0)
                {
                    MessageBox.Show(this, "Both fields are required.", "New tolerance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(10, 8, 10, 8) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public string SignalType => _signalType.Text.Trim();

        public string ModuleType => _moduleType.Text.Trim();
    }
}
