using Microsoft.ML.Data;

namespace CardboardQualityControl.ML
{
    public class ModelInput
    {
        [LoadColumn(0)]
        public string ImagePath { get; set; } = string.Empty;

        [LoadColumn(1)]
        public string Label { get; set; } = string.Empty;

        [ColumnName("input")]
        [VectorType(3, 224, 224)]
        public byte[] Image { get; set; } = Array.Empty<byte>();
    }
}