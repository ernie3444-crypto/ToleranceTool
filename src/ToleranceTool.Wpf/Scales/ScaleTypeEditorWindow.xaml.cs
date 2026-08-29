using System.Windows;

namespace ToleranceTool.Wpf.Scales
{
    public partial class ScaleTypeEditorWindow : Window
    {
        public ScaleTypeEditorWindow(string? path = null)
        {
            InitializeComponent();
            DataContext = new ScaleTypeEditorViewModel(path);
        }
    }
}
