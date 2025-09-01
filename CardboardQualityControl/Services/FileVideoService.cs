using Microsoft.Win32; // Добавьте этот using
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
        private string _currentVideoPath;

        public event Action<Mat>? FrameReady;
        public bool IsConnected => _capture?.IsOpened() == true;

        public string CurrentVideoPath
        {
            get => _currentVideoPath;
            private set => _currentVideoPath = value;
        }

        public FileVideoService(ILogger<FileVideoService> logger, FileVideoSettings settings)
        {
            _logger = logger;
            _settings = settings;
            _currentVideoPath = settings.Path;
        }

        public async Task<bool> ConnectAsync()
        {
            return await ConnectAsync(null);
        }

        public async Task<bool> ConnectAsync(string? filePath = null)
        {
            try
            {
                // Если путь не указан, открываем диалог выбора файла
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = ShowOpenFileDialog(); // Измените название метода
                    if (string.IsNullOrEmpty(filePath))
                    {
                        _logger.LogWarning("No video file selected");
                        return false;
                    }
                }

                _logger.LogInformation("Opening video file: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogError("Video file does not exist: {FilePath}", filePath);
                    return false;
                }

                _capture = new VideoCapture(filePath);
                if (!_capture.IsOpened())
                {
                    _logger.LogError("Failed to open video file: {FilePath}", filePath);
                    return false;
                }

                CurrentVideoPath = filePath;
                _logger.LogInformation("Video file opened successfully: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open video file");
                return false;
            }
        }

        private string? ShowOpenFileDialog() // Измените название метода
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.flv;*.webm|All Files|*.*",
                Title = "Select Video File",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }

            return null;
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

                _ = Task.Run(() => CaptureFrames(_cancellationTokenSource.Token));

                _logger.LogInformation("Started playing video file: {FilePath}", CurrentVideoPath);
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
                            // End of video, restart from beginning if loop is enabled
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