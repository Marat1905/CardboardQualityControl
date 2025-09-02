using System.Text.Json.Serialization;

namespace CardboardQualityControl.Models
{
    public class AppConfig
    {
        [JsonPropertyName("videoSource")]
        public string VideoSource { get; set; } = "Basler";

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

        [JsonPropertyName("videoRecordingSettings")]
        public VideoRecordingSettings VideoRecordingSettings { get; set; } = new();

        [JsonPropertyName("cameraSettings")]
        public CameraSettings CameraSettings { get; set; } = new();
    }

    public class CameraSettings
    {
        [JsonPropertyName("exposureTime")]
        public double ExposureTime { get; set; } = 10000;

        [JsonPropertyName("gain")]
        public double Gain { get; set; } = 1.0;

        [JsonPropertyName("pixelFormat")]
        public string PixelFormat { get; set; } = "BGR8";

        [JsonPropertyName("width")]
        public int Width { get; set; } = 1920;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 1080;

        [JsonPropertyName("fps")]
        public double FPS { get; set; } = 30.0;

        [JsonPropertyName("gamma")]
        public double Gamma { get; set; } = 1.0;

        [JsonPropertyName("brightness")]
        public double Brightness { get; set; } = 0.0;

        [JsonPropertyName("contrast")]
        public double Contrast { get; set; } = 1.0;
    }

    public class BaslerCameraSettings : CameraSettings
    {
        [JsonPropertyName("autoExposure")]
        public bool AutoExposure { get; set; } = true;

        [JsonPropertyName("autoGain")]
        public bool AutoGain { get; set; } = true;

        [JsonPropertyName("autoWhiteBalance")]
        public bool AutoWhiteBalance { get; set; } = true;
    }

    public class IpCameraSettings : CameraSettings
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "rtsp://username:password@ipaddress:port";

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
    }

    public class FileVideoSettings
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "C:\\Videos\\cardboard_defects.mp4";

        [JsonPropertyName("loop")]
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

        [JsonPropertyName("inputWidth")]
        public int InputWidth { get; set; } = 224;

        [JsonPropertyName("inputHeight")]
        public int InputHeight { get; set; } = 224;
    }

    public class VideoRecordingSettings
    {
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; } = "C:\\Recordings";

        [JsonPropertyName("codec")]
        public string Codec { get; set; } = "MJPG";

        [JsonPropertyName("fps")]
        public double FPS { get; set; } = 30.0;

        [JsonPropertyName("quality")]
        public int Quality { get; set; } = 95;

        [JsonPropertyName("maxDurationMinutes")]
        public int MaxDurationMinutes { get; set; } = 60;

        [JsonPropertyName("recordOnDefect")]
        public bool RecordOnDefect { get; set; } = true;

        [JsonPropertyName("preRecordSeconds")]
        public int PreRecordSeconds { get; set; } = 5;
    }
}