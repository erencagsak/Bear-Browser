using System;
using Microsoft.UI.Xaml.Data;

namespace Bear_Browser.Converters
{
    public class PasswordGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isVisible && isVisible)
            {
                return "\uED1A"; // Göz açık
            }
            return "\uE7B3"; // Göz kapalı / gizli
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
