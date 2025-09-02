using OpenCvSharp;
using Microsoft.Extensions.Logging;
using CardboardQualityControl.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace CardboardQualityControl.Services
{
    public class IpVideoService : IVideoService
    {
        private readonly ILogger<IpVideoService> _logger;
        private readonly IpCameraSettings _settings;
        private VideoCapture? _capture;
        private bool _isCapturing;
        private CancellationTokenSource? _cancellationTokenSource;
        private OpenCvSharp.VideoWriter? _videoWriter;
        private string? _currentRecordingPath;
        private double _fps;
        private double _currentPosition;
        private double _totalFrames;
        private DateTime _lastFrameTime;
        private int _frameCount;

        public event Action<Mat>? FrameReady;
        public bool IsConnected => _capture?.IsOpened() == true;
        public double FPS => _fps;
        public double CurrentPosition => _currentPosition;
        public double TotalFrames => _totalFrames;
        public bool IsRecording => _videoWriter != null && _videoWriter.IsOpened();

        public IpVideoService(ILogger<IpVideoService> logger, IpCameraSettings settings)
        {
            _logger = logger;
            _settings = settings;
            _fps = settings.FPS;
            _lastFrameTime = DateTime.Now;
        }

        public async Task<bool> ConnectAsync()
        {
            return await ConnectAsync(null);
        }

        public async Task<bool> ConnectAsync(string? filePath = null)
        {
            try
            {
                _logger.LogInformation("Connecting to IP camera...");

                // Build connection string with credentials if provided
                var connectionString = _settings.Url;
                if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
                {
                    var uri = new Uri(_settings.Url);
                    connectionString = $"{uri.Scheme}://{_settings.Username}:{_settings.Password}@{uri.Host}{uri.PathAndQuery}";
                }

                _capture = new VideoCapture(connectionString);

                // Set camera properties
                if (_capture.IsOpened())
                {
                    _capture.Set(VideoCaptureProperties.Fps, _settings.FPS);
                    _capture.Set(VideoCaptureProperties.FrameWidth, _settings.Width);
                    _capture.Set(VideoCaptureProperties.FrameHeight, _settings.Height);
                    _capture.Set(VideoCaptureProperties.Brightness, _settings.Brightness);
                    _capture.Set(VideoCaptureProperties.Contrast, _settings.Contrast);
                }

                if (!_capture.IsOpened())
                {
                    _logger.LogError("Failed to open IP camera stream");
                    return false;
                }

                _fps = _capture.Get(VideoCaptureProperties.Fps);
                _totalFrames = _capture.Get(VideoCaptureProperties.FrameCount);
                _logger.LogInformation("IP camera connected successfully. FPS: {FPS}", _fps);
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
            await StopRecordingAsync();
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

        public Task SeekAsync(double position)
        {
            if (_capture != null && _capture.IsOpened())
            {
                _capture.Set(VideoCaptureProperties.PosFrames, position);
                _currentPosition = position;
            }
            return Task.CompletedTask;
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
                            // Calculate FPS
                            var currentTime = DateTime.Now;
                            var elapsed = (currentTime - _lastFrameTime).TotalSeconds;
                            _frameCount++;

                            if (elapsed >= 1.0) // Update FPS every second
                            {
                                _fps = _frameCount / elapsed;
                                _frameCount = 0;
                                _lastFrameTime = currentTime;
                            }

                            _currentPosition = _capture.Get(VideoCaptureProperties.PosFrames);
                            FrameReady?.Invoke(frame.Clone());

                            // Write to video file if recording
                            if (IsRecording && _videoWriter != null)
                            {
                                _videoWriter.Write(frame);
                            }
                        }
                    }

                    await Task.Delay((int)(1000 / _fps), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing frame from IP camera");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public async Task StartRecordingAsync(string outputPath)
        {
            if (_capture == null || !_capture.IsOpened()) return;

            try
            {
                await StopRecordingAsync();

                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var frameWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                var frameHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                var frameSize = new OpenCvSharp.Size(frameWidth, frameHeight);

                // Use FourCC.FromString instead of Parse
                int fourcc = OpenCvSharp.VideoWriter.FourCC('M', 'J', 'P', 'G'); // MJPG codec

                _videoWriter = new OpenCvSharp.VideoWriter(
                    outputPath,
                    fourcc,
                    _fps,
                    frameSize,
                    true
                );

                if (_videoWriter.IsOpened())
                {
                    _currentRecordingPath = outputPath;
                    _logger.LogInformation($"Started recording to: {outputPath}");
                }
                else
                {
                    _logger.LogError("Failed to open video writer");
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start recording");
            }
        }

        public async Task StopRecordingAsync()
        {
            if (_videoWriter != null)
            {
                try
                {
                    _videoWriter.Release();
                    _videoWriter.Dispose();
                    _videoWriter = null;

                    _logger.LogInformation($"Stopped recording: {_currentRecordingPath}");
                    _currentRecordingPath = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping recording");
                }
            }
        }

        public void Dispose()
        {
            try
            {
                DisconnectAsync().Wait();
                _capture?.Dispose();
                _cancellationTokenSource?.Dispose();
                _videoWriter?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
        }
    }
}