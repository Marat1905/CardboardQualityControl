using CardboardQualityControl.Converters;
using CardboardQualityControl.ML;
using CardboardQualityControl.Models;
using CardboardQualityControl.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CardboardQualityControl.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IVideoService _videoService;
        private readonly QualityModelService _modelService;
        private readonly AppConfig _config;
        private readonly Dispatcher _dispatcher;
        private BitmapSource? _currentFrame;
        private DefectInfo? _currentDefect;
        private bool _isMonitoring;
        private string _statusMessage = "Ready";
        private int _defectCount;
        private int _totalFramesProcessed;

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        public ICommand CaptureTrainingImageCommand { get; }

        public MainViewModel(ILogger<MainViewModel> logger,
                     IVideoService videoService,
                     QualityModelService modelService,
                     AppConfig config,
                     System.Windows.Threading.Dispatcher dispatcher)
        {
            _logger = logger;
            _videoService = videoService;
            _modelService = modelService;
            _config = config;
            _dispatcher = dispatcher;

            // Setup commands
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
        }

        private async void OnFrameReady(Mat frame)
        {
            try
            {
                // Convert Mat to BitmapSource for display
                await _dispatcher.InvokeAsync(() =>
                {
                    CurrentFrame = BitmapSourceConverter.ToBitmapSource(frame);
                });

                // Process frame with model
                var prediction = _modelService.Predict(frame);
                TotalFramesProcessed++;

                // Update defect count if defect detected
                if (prediction.DefectType != DefectType.None &&
                    prediction.Confidence >= _config.ModelSettings.ConfidenceThreshold)
                {
                    DefectCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing frame");
            }
        }

        private void OnPredictionReady(ModelOutput prediction)
        {
            CurrentDefect = new DefectInfo
            {
                DefectType = prediction.DefectType,
                Confidence = prediction.Confidence
            };

            if (HasDefect)
            {
                StatusMessage = $"Defect detected: {prediction.DefectType} ({(prediction.Confidence * 100):F1}%)";
            }
            else
            {
                StatusMessage = "No defects detected";
            }
        }

        private async Task StartMonitoringAsync()
        {
            try
            {
                StatusMessage = "Connecting to video source...";

                if (!await _videoService.ConnectAsync())
                {
                    StatusMessage = "Failed to connect to video source";
                    return;
                }

                await _videoService.StartCaptureAsync();
                IsMonitoring = true;
                StatusMessage = "Monitoring started";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start monitoring");
                StatusMessage = "Error starting monitoring";
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
                var defectType = CurrentDefect?.DefectType.ToString() ?? "Unknown";
                var filename = Path.Combine(trainingDir, $"{defectType}_{timestamp}.jpg");

                // Save current frame
                using (var fileStream = new FileStream(filename, FileMode.Create))
                {
                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(CurrentFrame));
                    encoder.Save(fileStream);
                }

                StatusMessage = $"Training image saved: {filename}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture training image");
                StatusMessage = "Error capturing training image";
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}