using System.Globalization;
using System.Windows.Data;

namespace CardboardQualityControl.Converters
{
    public class BooleanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue ?
                (boolValue ? "DEFECT DETECTED" : "No defects") :
                "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}