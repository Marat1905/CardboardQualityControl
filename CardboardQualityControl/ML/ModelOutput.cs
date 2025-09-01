using Microsoft.ML.Data;

namespace CardboardQualityControl.ML
{
    public class ModelOutput
    {
        [ColumnName("output")]
        public float[] Scores { get; set; } = Array.Empty<float>();

        [ColumnName("softmax2")]
        public float[] Probabilities { get; set; } = Array.Empty<float>();

        public string PredictedLabel { get; set; } = string.Empty;

        public DefectType DefectType { get; set; }

        public float Confidence { get; set; }
    }
}