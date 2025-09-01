using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CardboardQualityControl.ML;

namespace CardboardQualityControl.Converters
{
    public class DefectToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DefectType defectType)
                return Brushes.Transparent;

            return defectType switch
            {
                DefectType.None => Brushes.Green,
                DefectType.Hole => Brushes.Red,
                DefectType.Tear => Brushes.Orange,
                DefectType.Stain => Brushes.Yellow,
                DefectType.Wrinkle => Brushes.Purple,
                DefectType.ForeignObject => Brushes.Brown,
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}