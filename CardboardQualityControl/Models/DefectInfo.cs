using CardboardQualityControl.ML;

namespace CardboardQualityControl.Models
{
    public class DefectInfo
    {
        public DefectType DefectType { get; set; }
        public float Confidence { get; set; }
        public DateTime Timestamp { get; set; }
    }
}