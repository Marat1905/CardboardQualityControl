using OpenCvSharp;

namespace CardboardQualityControl.Services
{
    public interface IVideoService : IDisposable
    {
        event Action<Mat> FrameReady;
        bool IsConnected { get; }
        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task StartCaptureAsync();
        Task StopCaptureAsync();
    }
}