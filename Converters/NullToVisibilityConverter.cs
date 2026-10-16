using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Bear_Browser.Converters
{
    /// <summary>
    /// Nesnenin null olup olmamasına göre Visibility döner (Null değilse Visible, null ise Collapsed).
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string s)
            {
                return !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;
            }
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
