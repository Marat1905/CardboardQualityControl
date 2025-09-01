using OpenCvSharp;
using Microsoft.Extensions.Logging;
using CardboardQualityControl.Models;

namespace CardboardQualityControl.Services
{
    public class IpVideoService : IVideoService
    {
        private readonly ILogger<IpVideoService> _logger;
        private readonly IpCameraSettings _settings;
        private VideoCapture? _capture;
        private bool _isCapturing;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<Mat>? FrameReady;
        public bool IsConnected => _capture?.IsOpened() == true;

        public IpVideoService(ILogger<IpVideoService> logger, IpCameraSettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.LogInformation("Connecting to IP camera...");

                _capture = new VideoCapture(_settings.Url);
                if (!_capture.IsOpened())
                {
                    _logger.LogError("Failed to open IP camera stream");
                    return false;
                }

                _logger.LogInformation("IP camera connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to IP camera");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            await StopCaptureAsync();
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
            _logger.LogInformation("IP camera disconnected");
        }

        public async Task StartCaptureAsync()
        {
            if (_capture == null || !_capture.IsOpened())
            {
                _logger.LogWarning("Camera is not connected");
                return;
            }

            if (_isCapturing) return;

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _isCapturing = true;

                // Start frame capture loop
                _ = Task.Run(() => CaptureFrames(_cancellationTokenSource.Token));

                _logger.LogInformation("Started capturing from IP camera");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start capturing from IP camera");
            }
        }

        public async Task StopCaptureAsync()
        {
            if (!_isCapturing) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _isCapturing = false;
                _logger.LogInformation("Stopped capturing from IP camera");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop capturing from IP camera");
            }
        }

        private async Task CaptureFrames(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _capture != null && _capture.IsOpened())
            {
                try
                {
                    using (var frame = new Mat())
                    {
                        if (_capture.Read(frame) && !frame.Empty())
                        {
                            FrameReady?.Invoke(frame.Clone());
                        }
                    }

                    await Task.Delay(33, cancellationToken); // ~30 FPS
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing frame from IP camera");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _capture?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}