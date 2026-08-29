using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ToleranceTool.Wpf
{
    /// <summary>True when the bound value is non-null (e.g. to enable a panel only when something is selected).</summary>
    public sealed class NotNullConverter : IValueConverter
    {
        public static readonly NotNullConverter Instance = new NotNullConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Green for a true (valid) value, firebrick for false.</summary>
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

    /// <summary>Collapses an element when the bound value is null / empty string / false.</summary>
    public sealed class VisibleWhenConverter : IValueConverter
    {
        public static readonly VisibleWhenConverter Instance = new VisibleWhenConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visible = value switch
            {
                null => false,
                bool b => b,
                string s => s.Length > 0,
                _ => true,
            };

            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
