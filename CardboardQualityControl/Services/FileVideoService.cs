using Microsoft.Win32;
using OpenCvSharp;
using Microsoft.Extensions.Logging;
using CardboardQualityControl.Models;
using System.IO;
using System.Diagnostics;

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
        private bool _isDialogOpen;
        private readonly object _captureLock = new object();
        private Stopwatch _frameTimer;
        private double _frameDelayMs;

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
            _isDialogOpen = false;
            _frameTimer = new Stopwatch();
        }

        public async Task<bool> ConnectAsync()
        {
            return await ConnectAsync(null);
        }

        public async Task<bool> ConnectAsync(string? filePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = ShowOpenFileDialog();
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

                lock (_captureLock)
                {
                    _capture = new VideoCapture(filePath);
                    if (!_capture.IsOpened())
                    {
                        _logger.LogError("Failed to open video file: {FilePath}", filePath);
                        _capture.Dispose();
                        _capture = null;
                        return false;
                    }

                    // Рассчитываем задержку между кадрами на основе FPS
                    double fps = _capture.Fps;
                    _frameDelayMs = fps > 0 ? 1000.0 / fps : 33.33;
                    _logger.LogInformation("Video FPS: {FPS}, Frame delay: {Delay}ms", fps, _frameDelayMs);
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

        private string? ShowOpenFileDialog()
        {
            if (_isDialogOpen) return null;

            _isDialogOpen = true;
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.flv;*.webm|All Files|*.*",
                    Title = "Select Video File",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    Multiselect = false
                };

                return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in file dialog");
                return null;
            }
            finally
            {
                _isDialogOpen = false;
            }
        }

        public async Task DisconnectAsync()
        {
            await StopCaptureAsync();
            lock (_captureLock)
            {
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }
            _frameTimer.Stop();
            _logger.LogInformation("Video file closed");
        }

        public async Task StartCaptureAsync()
        {
            lock (_captureLock)
            {
                if (_capture == null || !_capture.IsOpened())
                {
                    _logger.LogWarning("Video file is not open");
                    return;
                }

                if (_isCapturing) return;
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _isCapturing = true;
                _frameTimer.Restart();

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
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _isCapturing = false;
                _frameTimer.Stop();

                _logger.LogInformation("Stopped playing video file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop playing video file");
            }
        }

        private async Task CaptureFrames(CancellationToken cancellationToken)
        {
            var stopwatch = new Stopwatch();
            var targetFrameTime = TimeSpan.FromMilliseconds(_frameDelayMs);

            while (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Restart();

                try
                {
                    VideoCapture? localCapture;
                    lock (_captureLock)
                    {
                        localCapture = _capture;
                    }

                    if (localCapture == null || !localCapture.IsOpened())
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    // Проверяем конец видео
                    double currentFramePos = 0;
                    double totalFrames = 0;

                    lock (_captureLock)
                    {
                        currentFramePos = localCapture.Get(VideoCaptureProperties.PosFrames);
                        totalFrames = localCapture.Get(VideoCaptureProperties.FrameCount);
                    }

                    // Если достигли конца видео, перематываем в начало
                    if (currentFramePos >= totalFrames - 1)
                    {
                        lock (_captureLock)
                        {
                            localCapture.Set(VideoCaptureProperties.PosFrames, 0);
                        }
                        continue;
                    }

                    using (var frame = new Mat())
                    {
                        bool readSuccess;
                        lock (_captureLock)
                        {
                            readSuccess = localCapture.Read(frame) && !frame.Empty();
                        }

                        if (!readSuccess)
                        {
                            // Если не удалось прочитать, перематываем в начало
                            lock (_captureLock)
                            {
                                localCapture.Set(VideoCaptureProperties.PosFrames, 0);
                            }
                            continue;
                        }

                        // Отправляем кадр
                        FrameReady?.Invoke(frame.Clone());
                    }

                    // Точная синхронизация времени
                    stopwatch.Stop();
                    var elapsed = stopwatch.Elapsed;
                    var remainingTime = targetFrameTime - elapsed;

                    if (remainingTime > TimeSpan.Zero)
                    {
                        await Task.Delay(remainingTime, cancellationToken);
                    }
                    else
                    {
                        // Если обработка заняла больше времени, чем нужно для одного кадра,
                        // продолжаем без задержки, но логируем предупреждение
                        _logger.LogWarning("Frame processing too slow: {ElapsedMs}ms vs target {TargetMs}ms",
                            elapsed.TotalMilliseconds, targetFrameTime.TotalMilliseconds);
                        await Task.Yield();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
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
            try
            {
                var disconnectTask = DisconnectAsync();
                if (!disconnectTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("Disconnect timed out");
                }

                lock (_captureLock)
                {
                    _capture?.Dispose();
                    _capture = null;
                }

                _cancellationTokenSource?.Dispose();
                _frameTimer.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
        }
    }
}