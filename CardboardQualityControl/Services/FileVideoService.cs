using OpenCvSharp;
using Microsoft.Extensions.Logging;
using CardboardQualityControl.Models;
using System.IO;

namespace CardboardQualityControl.Services
{
    public class FileVideoService : IVideoService
    {
        private readonly ILogger<FileVideoService> _logger;
        private readonly FileVideoSettings _settings;
        private VideoCapture? _capture;
        private bool _isCapturing;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<Mat>? FrameReady;
        public bool IsConnected => _capture?.IsOpened() == true;

        public FileVideoService(ILogger<FileVideoService> logger, FileVideoSettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.LogInformation("Opening video file...");

                if (!File.Exists(_settings.Path))
                {
                    _logger.LogError("Video file does not exist: {Path}", _settings.Path);
                    return false;
                }

                _capture = new VideoCapture(_settings.Path);
                if (!_capture.IsOpened())
                {
                    _logger.LogError("Failed to open video file");
                    return false;
                }

                _logger.LogInformation("Video file opened successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open video file");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            await StopCaptureAsync();
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
            _logger.LogInformation("Video file closed");
        }

        public async Task StartCaptureAsync()
        {
            if (_capture == null || !_capture.IsOpened())
            {
                _logger.LogWarning("Video file is not open");
                return;
            }

            if (_isCapturing) return;

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _isCapturing = true;

                // Start frame capture loop
                _ = Task.Run(() => CaptureFrames(_cancellationTokenSource.Token));

                _logger.LogInformation("Started playing video file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start playing video file");
            }
        }

        public async Task StopCaptureAsync()
        {
            if (!_isCapturing) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _isCapturing = false;

                // Reset to beginning of video
                _capture?.Set(VideoCaptureProperties.PosFrames, 0);

                _logger.LogInformation("Stopped playing video file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop playing video file");
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
                        else
                        {
                            // End of video, restart from beginning
                            _capture.Set(VideoCaptureProperties.PosFrames, 0);
                        }
                    }

                    // Adjust delay based on video FPS
                    var fps = _capture.Fps;
                    var delay = fps > 0 ? (int)(1000 / fps) : 33;
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading frame from video file");
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