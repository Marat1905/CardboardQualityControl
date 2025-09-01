using CardboardQualityControl.ML;
using CardboardQualityControl.Models;
using CardboardQualityControl.Services;
using CardboardQualityControl.Converters;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO;

namespace CardboardQualityControl.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly VideoServiceFactory _videoServiceFactory;
        private readonly QualityModelService _modelService;
        private readonly AppConfig _config;
        private readonly Dispatcher _dispatcher;

        private IVideoService _videoService;
        private BitmapSource? _currentFrame;
        private DefectInfo? _currentDefect;
        private bool _isMonitoring;
        private string _statusMessage = "Ready";
        private int _defectCount;
        private int _totalFramesProcessed;
        private VideoSourceType _selectedVideoSource;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<VideoSourceType> AvailableVideoSources { get; } = new()
        {
            VideoSourceType.Basler,
            VideoSourceType.IpCamera,
            VideoSourceType.FileVideo
        };

        public VideoSourceType SelectedVideoSource
        {
            get => _selectedVideoSource;
            set
            {
                if (_selectedVideoSource != value)
                {
                    _selectedVideoSource = value;
                    OnPropertyChanged();
                }
            }
        }

        public BitmapSource? CurrentFrame
        {
            get => _currentFrame;
            set
            {
                _currentFrame = value;
                OnPropertyChanged();
            }
        }

        public DefectInfo? CurrentDefect
        {
            get => _currentDefect;
            set
            {
                _currentDefect = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDefect));
            }
        }

        public bool HasDefect => CurrentDefect != null &&
                               CurrentDefect.DefectType != DefectType.None &&
                               CurrentDefect.Confidence >= _config.ModelSettings.ConfidenceThreshold;

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                _isMonitoring = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int DefectCount
        {
            get => _defectCount;
            set
            {
                _defectCount = value;
                OnPropertyChanged();
            }
        }

        public int TotalFramesProcessed
        {
            get => _totalFramesProcessed;
            set
            {
                _totalFramesProcessed = value;
                OnPropertyChanged();
            }
        }

        public ICommand SwitchVideoSourceCommand { get; }
        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        public ICommand CaptureTrainingImageCommand { get; }

        public MainViewModel(ILogger<MainViewModel> logger,
                           VideoServiceFactory videoServiceFactory,
                           QualityModelService modelService,
                           AppConfig config,
                           Dispatcher dispatcher)
        {
            _logger = logger;
            _videoServiceFactory = videoServiceFactory;
            _modelService = modelService;
            _config = config;
            _dispatcher = dispatcher;

            SelectedVideoSource = _config.CurrentVideoSourceType;
            _videoService = _videoServiceFactory.CreateVideoService(SelectedVideoSource);

            // Setup commands
            SwitchVideoSourceCommand = new RelayCommand(async param =>
                await SwitchVideoSourceAsync((VideoSourceType)param));

            StartMonitoringCommand = new RelayCommand(async _ => await StartMonitoringAsync(),
                _ => !IsMonitoring);

            StopMonitoringCommand = new RelayCommand(async _ => await StopMonitoringAsync(),
                _ => IsMonitoring);

            CaptureTrainingImageCommand = new RelayCommand(async _ => await CaptureTrainingImageAsync(),
                _ => IsMonitoring && CurrentFrame != null);

            // Subscribe to events
            _videoService.FrameReady += OnFrameReady;
            _modelService.PredictionReady += OnPredictionReady;

            // Initialize model
            if (!_modelService.LoadModel())
            {
                StatusMessage = "Failed to load model";
            }
            else
            {
                StatusMessage = "Model loaded successfully. Ready to start monitoring.";
            }
        }

        private async Task SwitchVideoSourceAsync(VideoSourceType newSource)
        {
            try
            {
                if (IsMonitoring)
                {
                    await StopMonitoringAsync();
                }

                StatusMessage = $"Switching to {newSource}...";

                // Отписываемся от событий текущего сервиса
                if (_videoService != null)
                {
                    _videoService.FrameReady -= OnFrameReady;
                    _videoService.Dispose();
                }

                // Создаем новый сервис
                _videoService = _videoServiceFactory.CreateVideoService(newSource);
                SelectedVideoSource = newSource;

                // Подписываемся на события нового сервиса
                _videoService.FrameReady += OnFrameReady;

                // Очищаем текущий кадр
                await _dispatcher.InvokeAsync(() =>
                {
                    CurrentFrame = null;
                    CurrentDefect = null;
                });

                StatusMessage = $"Video source switched to: {newSource}";
                _logger.LogInformation("Video source switched to: {Source}", newSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch video source to {Source}", newSource);
                StatusMessage = $"Error switching to {newSource}: {ex.Message}";
            }
        }

        private async void OnFrameReady(Mat frame)
        {
            try
            {
                using (frame)
                {
                    // Convert Mat to BitmapSource for display
                    var bitmapSource = BitmapSourceConverter.ToBitmapSource(frame);

                    await _dispatcher.InvokeAsync(() =>
                    {
                        CurrentFrame = bitmapSource;
                    });

                    // Process frame with model
                    var prediction = _modelService.Predict(frame.Clone());
                    TotalFramesProcessed++;

                    // Update defect count if defect detected
                    if (prediction.DefectType != DefectType.None &&
                        prediction.Confidence >= _config.ModelSettings.ConfidenceThreshold)
                    {
                        DefectCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing frame");
            }
        }

        private void OnPredictionReady(ModelOutput prediction)
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    CurrentDefect = new DefectInfo
                    {
                        DefectType = prediction.DefectType,
                        Confidence = prediction.Confidence,
                        Location = new System.Drawing.Rectangle(0, 0, 0, 0) // Можно добавить реальные координаты
                    };

                    if (HasDefect)
                    {
                        StatusMessage = $"Defect detected: {prediction.DefectType} ({(prediction.Confidence * 100):F1}%)";
                    }
                    else
                    {
                        StatusMessage = "No defects detected";
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating prediction UI");
            }
        }

        private async Task StartMonitoringAsync()
        {
            try
            {
                StatusMessage = $"Connecting to {SelectedVideoSource}...";

                if (!await _videoService.ConnectAsync())
                {
                    StatusMessage = $"Failed to connect to {SelectedVideoSource}";
                    return;
                }

                await _videoService.StartCaptureAsync();
                IsMonitoring = true;
                StatusMessage = $"Monitoring started using {SelectedVideoSource}";
                _logger.LogInformation("Monitoring started using {Source}", SelectedVideoSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start monitoring with {Source}", SelectedVideoSource);
                StatusMessage = $"Error starting monitoring: {ex.Message}";
            }
        }

        private async Task StopMonitoringAsync()
        {
            try
            {
                await _videoService.StopCaptureAsync();
                await _videoService.DisconnectAsync();
                IsMonitoring = false;
                StatusMessage = "Monitoring stopped";
                _logger.LogInformation("Monitoring stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop monitoring");
                StatusMessage = "Error stopping monitoring";
            }
        }

        private async Task CaptureTrainingImageAsync()
        {
            try
            {
                if (CurrentFrame == null) return;

                // Create training directory if it doesn't exist
                var trainingDir = _config.TrainingSettings.TrainingDataPath;
                if (!Directory.Exists(trainingDir))
                {
                    Directory.CreateDirectory(trainingDir);
                }

                // Generate filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var defectType = CurrentDefect?.DefectType.ToString() ?? "None";
                var filename = Path.Combine(trainingDir, $"{defectType}_{timestamp}.jpg");

                // Save current frame
                using (var fileStream = new FileStream(filename, FileMode.Create))
                {
                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(CurrentFrame));
                    encoder.Save(fileStream);
                }

                StatusMessage = $"Training image saved: {Path.GetFileName(filename)}";
                _logger.LogInformation("Training image saved: {Filename}", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture training image");
                StatusMessage = "Error capturing training image";
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                StatusMessage = "Initializing application...";

                // Предварительное подключение к видео источнику
                if (await _videoService.ConnectAsync())
                {
                    StatusMessage = $"{SelectedVideoSource} connected. Ready to start monitoring.";
                }
                else
                {
                    StatusMessage = $"Failed to connect to {SelectedVideoSource}. Please check settings.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize application");
                StatusMessage = "Initialization failed";
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            try
            {
                if (_videoService != null)
                {
                    _videoService.FrameReady -= OnFrameReady;
                    _videoService.Dispose();
                }

                if (_modelService != null)
                {
                    _modelService.PredictionReady -= OnPredictionReady;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
        }
    }
}