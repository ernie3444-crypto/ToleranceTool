using System.Windows;

namespace ToleranceTool.Wpf.Aliases
{
    public partial class AliasTableEditorWindow : Window
    {
        public AliasTableEditorWindow(string? path = null)
        {
            InitializeComponent();
            DataContext = new AliasTableEditorViewModel(path);
        }
    }
}
