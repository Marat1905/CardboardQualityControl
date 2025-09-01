using System.Globalization;
using System.Windows.Data;
using CardboardQualityControl.ML;

namespace CardboardQualityControl.Converters
{
    public class DefectTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DefectType defectType)
            {
                return defectType switch
                {
                    DefectType.None => "None",
                    DefectType.Hole => "Hole",
                    DefectType.Tear => "Tear",
                    DefectType.Stain => "Stain",
                    DefectType.Wrinkle => "Wrinkle",
                    DefectType.ForeignObject => "Foreign Object",
                    DefectType.Unknown => "Unknown",
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