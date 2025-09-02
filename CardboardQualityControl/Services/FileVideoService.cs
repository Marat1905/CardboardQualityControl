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
        private OpenCvSharp.VideoWriter? _videoWriter;
        private string? _currentRecordingPath;
        private double _fps;
        private double _currentPosition;
        private double _totalFrames;

        public event Action<Mat>? FrameReady;
        public bool IsConnected => _capture?.IsOpened() == true;
        public double FPS => _fps;
        public double CurrentPosition => _currentPosition;
        public double TotalFrames => _totalFrames;
        public bool IsRecording => _videoWriter != null && _videoWriter.IsOpened();

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
                    _fps = _capture.Fps;
                    _frameDelayMs = _fps > 0 ? 1000.0 / _fps : 33.33;
                    _totalFrames = _capture.Get(VideoCaptureProperties.FrameCount);
                    _currentPosition = 0;

                    _logger.LogInformation("Video FPS: {FPS}, Frame delay: {Delay}ms, Total frames: {TotalFrames}",
                        _fps, _frameDelayMs, _totalFrames);
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
            await StopRecordingAsync();
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

        public async Task SeekAsync(double position)
        {
            lock (_captureLock)
            {
                if (_capture != null && _capture.IsOpened())
                {
                    _capture.Set(VideoCaptureProperties.PosFrames, position);
                    _currentPosition = position;
                }
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

                    double currentFramePos;
                    lock (_captureLock)
                    {
                        currentFramePos = localCapture.Get(VideoCaptureProperties.PosFrames);
                        _currentPosition = currentFramePos;
                    }

                    if (currentFramePos >= _totalFrames - 1)
                    {
                        if (_settings.LoopVideo)
                        {
                            lock (_captureLock)
                            {
                                localCapture.Set(VideoCaptureProperties.PosFrames, 0);
                            }
                        }
                        else
                        {
                            await StopCaptureAsync();
                            break;
                        }
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
                            if (_settings.LoopVideo)
                            {
                                lock (_captureLock)
                                {
                                    localCapture.Set(VideoCaptureProperties.PosFrames, 0);
                                }
                            }
                            continue;
                        }

                        FrameReady?.Invoke(frame.Clone());

                        // Write to video file if recording
                        if (IsRecording && _videoWriter != null)
                        {
                            _videoWriter.Write(frame);
                        }
                    }

                    stopwatch.Stop();
                    var elapsed = stopwatch.Elapsed;
                    var remainingTime = targetFrameTime - elapsed;

                    if (remainingTime > TimeSpan.Zero)
                    {
                        await Task.Delay(remainingTime, cancellationToken);
                    }
                    else
                    {
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

        public async Task StartRecordingAsync(string outputPath)
        {
            try
            {
                await StopRecordingAsync();

                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (_captureLock)
                {
                    if (_capture != null)
                    {
                        var frameSize = new Size(
                            (int)_capture.Get(VideoCaptureProperties.FrameWidth),
                            (int)_capture.Get(VideoCaptureProperties.FrameHeight)
                        );

                        // Используем FourCC.FromString вместо FourCC.Parse
                        int fourcc = OpenCvSharp.VideoWriter.FourCC('M', 'J', 'P', 'G'); // MJPG codec для совместимости

                        _videoWriter = new OpenCvSharp.VideoWriter(
                            outputPath,
                            fourcc,
                            _fps,
                            frameSize,
                            true // isColor
                        );

                        if (_videoWriter.IsOpened())
                        {
                            _currentRecordingPath = outputPath;
                            _logger.LogInformation($"Started recording to: {outputPath}");
                        }
                        else
                        {
                            _logger.LogError("Failed to open video writer");
                            _videoWriter.Dispose();
                            _videoWriter = null;
                        }
                    }
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