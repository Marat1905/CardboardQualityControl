using OpenCvSharp;

namespace CardboardQualityControl.Services
{
    public interface IVideoService : IDisposable
    {
        event Action<Mat> FrameReady;
        bool IsConnected { get; }
        double FPS { get; }
        double CurrentPosition { get; }
        double TotalFrames { get; }
        bool IsRecording { get; }

        Task<bool> ConnectAsync();
        Task<bool> ConnectAsync(string? filePath = null);
        Task DisconnectAsync();
        Task StartCaptureAsync();
        Task StopCaptureAsync();
        Task StartRecordingAsync(string outputPath);
        Task StopRecordingAsync();
        Task SeekAsync(double position);
    }
}