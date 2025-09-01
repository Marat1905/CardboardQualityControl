using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;

namespace CardboardQualityControl.Converters
{
    public class DefectToStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool hasDefect && hasDefect ?
                new SolidColorBrush(Colors.Red) :
                new SolidColorBrush(Colors.Green);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}