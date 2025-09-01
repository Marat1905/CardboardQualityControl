using System.Globalization;
using System.Windows.Data;
using CardboardQualityControl.Models;

namespace CardboardQualityControl.Converters
{
    public class VideoSourceTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is VideoSourceType sourceType)
            {
                return sourceType switch
                {
                    VideoSourceType.Basler => "Basler Camera",
                    VideoSourceType.IpCamera => "IP Camera",
                    VideoSourceType.FileVideo => "Video File",
                    _ => "Unknown"
                };
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}