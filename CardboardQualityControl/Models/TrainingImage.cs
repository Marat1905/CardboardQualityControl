using CardboardQualityControl.ML;

namespace CardboardQualityControl.Models
{
    public class TrainingImage
    {
        public string ImagePath { get; set; } = string.Empty;
        public DefectType DefectType { get; set; }
    }
}