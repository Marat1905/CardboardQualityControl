using OpenCvSharp;

namespace CardboardQualityControl.Services
{
    public interface IVideoService : IDisposable
    {
        event Action<Mat> FrameReady;
        bool IsConnected { get; }

        Task<bool> ConnectAsync(); // Без параметров
        Task<bool> ConnectAsync(string? filePath = null); // С опциональным параметром
        Task DisconnectAsync();
        Task StartCaptureAsync();
        Task StopCaptureAsync();
    }
}