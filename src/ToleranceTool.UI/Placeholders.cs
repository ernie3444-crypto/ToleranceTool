using System.Windows.Forms;

namespace ToleranceTool.UI
{
    /// <summary>
    /// Temporary stand-in for the setup screens (Datasheet Mapping, Signal Import,
    /// Alias Tables, Tolerance Editor, Scale Types, Signal Types). Replaced feature
    /// by feature from P2 onward.
    /// </summary>
    public static class Placeholders
    {
        public static void NotImplemented(string feature)
        {
            MessageBox.Show(
                feature + " is not implemented yet.",
                "Tolerance Tool",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
