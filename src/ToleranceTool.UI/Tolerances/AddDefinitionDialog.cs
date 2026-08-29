using System;
using System.Drawing;
using System.Windows.Forms;

namespace ToleranceTool.UI.Tolerances
{
    /// <summary>Prompts for the signal type + module type of a new tolerance definition.</summary>
    public sealed class AddDefinitionDialog : Form
    {
        private readonly TextBox _signalType = new TextBox { Width = 220 };
        private readonly TextBox _moduleType = new TextBox { Width = 220 };

        public AddDefinitionDialog()
        {
            Text = "New tolerance";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(320, 140);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
            layout.Controls.Add(new Label { Text = "Signal type", AutoSize = true, Margin = new Padding(3, 8, 12, 3) }, 0, 0);
            layout.Controls.Add(_signalType, 1, 0);
            layout.Controls.Add(new Label { Text = "Module type", AutoSize = true, Margin = new Padding(3, 8, 12, 3) }, 0, 1);
            layout.Controls.Add(_moduleType, 1, 1);

            var ok = new Button { Text = "OK", Width = 80 };
            var cancel = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
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

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(8) };
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
