using System.Windows;

namespace ToleranceTool.Wpf.SignalTypes
{
    public partial class SignalTypeEditorWindow : Window
    {
        public SignalTypeEditorWindow(string? path = null)
        {
            InitializeComponent();
            DataContext = new SignalTypeEditorViewModel(path);
        }
    }
}
