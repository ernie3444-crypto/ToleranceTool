using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

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

    /// <summary>Enables a control only when the bound value is non-null.</summary>
    public sealed class NotNullConverter : IValueConverter
    {
        public static readonly NotNullConverter Instance = new NotNullConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Green when the curve is valid, firebrick when not.</summary>
    public sealed class ValidBrushConverter : IValueConverter
    {
        public static readonly ValidBrushConverter Instance = new ValidBrushConverter();

        private static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x1B, 0x7A, 0x2F));
        private static readonly Brush Bad = new SolidColorBrush(Color.FromRgb(0xB2, 0x22, 0x22));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && b ? Ok : Bad;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
