using System.Text.Json.Serialization;

namespace CardboardQualityControl.Models
{
    public class AppConfig
    {
        [JsonPropertyName("videoSource")]
        public string VideoSource { get; set; } = "Basler";

        // Добавьте свойство для текущего типа источника
        public VideoSourceType CurrentVideoSourceType
        {
            get
            {
                return VideoSource.ToLower() switch
                {
                    "basler" => VideoSourceType.Basler,
                    "ip" => VideoSourceType.IpCamera,
                    "file" => VideoSourceType.FileVideo,
                    _ => VideoSourceType.Basler
                };
            }
        }

        [JsonPropertyName("baslerCameraSettings")]
        public BaslerCameraSettings BaslerCameraSettings { get; set; } = new();

        [JsonPropertyName("ipCameraSettings")]
        public IpCameraSettings IpCameraSettings { get; set; } = new();

        [JsonPropertyName("fileVideoSettings")]
        public FileVideoSettings FileVideoSettings { get; set; } = new();

        [JsonPropertyName("modelSettings")]
        public ModelSettings ModelSettings { get; set; } = new();

        [JsonPropertyName("trainingSettings")]
        public TrainingSettings TrainingSettings { get; set; } = new();
    }

    public class BaslerCameraSettings
    {
        [JsonPropertyName("exposureTime")]
        public long ExposureTime { get; set; } = 10000;

        [JsonPropertyName("gain")]
        public double Gain { get; set; } = 1.0;
    }

    public class IpCameraSettings
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "rtsp://username:password@ipaddress:port";
    }

    public class FileVideoSettings
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "C:\\Videos\\cardboard_defects.mp4";

        [JsonPropertyName("Loop")]
        public bool LoopVideo { get; set; } = false;
    }

    public class ModelSettings
    {
        [JsonPropertyName("modelPath")]
        public string ModelPath { get; set; } = "quality-model.zip";

        [JsonPropertyName("inputWidth")]
        public int InputWidth { get; set; } = 224;

        [JsonPropertyName("inputHeight")]
        public int InputHeight { get; set; } = 224;

        [JsonPropertyName("confidenceThreshold")]
        public float ConfidenceThreshold { get; set; } = 0.7f;
    }

    public class TrainingSettings
    {
        [JsonPropertyName("trainingDataPath")]
        public string TrainingDataPath { get; set; } = "training-data";

        [JsonPropertyName("batchSize")]
        public int BatchSize { get; set; } = 10;

        [JsonPropertyName("epochs")]
        public int Epochs { get; set; } = 20;

        // Добавьте эти свойства
        [JsonPropertyName("inputWidth")]
        public int InputWidth { get; set; } = 224;

        [JsonPropertyName("inputHeight")]
        public int InputHeight { get; set; } = 224;
    }
}