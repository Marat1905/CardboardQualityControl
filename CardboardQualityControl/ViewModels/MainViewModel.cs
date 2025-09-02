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
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using CardboardQualityControl.Views;
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
        private readonly IServiceProvider _serviceProvider;

        private IVideoService _videoService;
        private BitmapSource? _currentFrame;
        private DefectInfo? _currentDefect;
        private bool _isMonitoring;
        private string _statusMessage = "Ready";
        private int _defectCount;
        private int _totalFramesProcessed;
        private VideoSourceType _selectedVideoSource;
        private string _currentVideoPath = string.Empty;
        private bool _isSelectingFile; // Флаг для отслеживания выбора файла

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
                    OnPropertyChanged(nameof(IsFileVideoSelected));
                }
            }
        }

        public bool IsFileVideoSelected => SelectedVideoSource == VideoSourceType.FileVideo;

        public string CurrentVideoPath
        {
            get => _currentVideoPath;
            set
            {
                _currentVideoPath = value;
                OnPropertyChanged();
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
        public ICommand SelectVideoFileCommand { get; }
        public ICommand OpenTrainingDialogCommand { get; }
        public ICommand ResetCountersCommand { get; }

        public MainViewModel(ILogger<MainViewModel> logger,
                           VideoServiceFactory videoServiceFactory,
                           QualityModelService modelService,
                           AppConfig config,
                           Dispatcher dispatcher,
                           IServiceProvider serviceProvider)
        {
            _logger = logger;
            _videoServiceFactory = videoServiceFactory;
            _modelService = modelService;
            _config = config;
            _dispatcher = dispatcher;
            _serviceProvider = serviceProvider;
            _isSelectingFile = false;

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

            SelectVideoFileCommand = new RelayCommand(async _ => await SelectVideoFileAsync(),
                _ => SelectedVideoSource == VideoSourceType.FileVideo && !_isSelectingFile); // Добавлена проверка

            OpenTrainingDialogCommand = new RelayCommand(_ => OpenTrainingDialog());
            ResetCountersCommand = new RelayCommand(_ => ResetCounters());

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

        private async Task SelectVideoFileAsync()
        {
            if (_isSelectingFile) return;

            try
            {
                _isSelectingFile = true;

                if (IsMonitoring)
                {
                    await StopMonitoringAsync();
                }

                // Создаем новый файловый сервис
                var fileVideoService = _videoServiceFactory.CreateVideoService(VideoSourceType.FileVideo) as FileVideoService;
                if (fileVideoService == null)
                {
                    StatusMessage = "File video service not available";
                    return;
                }

                // Вызываем ConnectAsync с null для открытия диалога
                bool connected = await fileVideoService.ConnectAsync(null);

                if (connected)
                {
                    CurrentVideoPath = fileVideoService.CurrentVideoPath;
                    StatusMessage = $"Selected video: {Path.GetFileName(CurrentVideoPath)}";

                    // Обновляем сервис
                    if (_videoService != null)
                    {
                        _videoService.FrameReady -= OnFrameReady;
                        _videoService.Dispose();
                    }

                    _videoService = fileVideoService;
                    _videoService.FrameReady += OnFrameReady;

                    // АВТОМАТИЧЕСКИ ЗАПУСКАЕМ МОНИТОРИНГ после выбора файла
                    await StartMonitoringAfterFileSelect();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select video file");
                StatusMessage = "Error selecting video file";
            }
            finally
            {
                _isSelectingFile = false;
            }
        }

        private async Task StartMonitoringAfterFileSelect()
        {
            try
            {
                if (_videoService != null && _videoService.IsConnected)
                {
                    await _videoService.StartCaptureAsync();
                    IsMonitoring = true;
                    StatusMessage = $"Monitoring started using {SelectedVideoSource}";
                    _logger.LogInformation("Monitoring started using {Source}", SelectedVideoSource);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start monitoring after file selection");
                StatusMessage = "Error starting monitoring";
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

                // Сбрасываем путь к файлу если это не файловый источник
                if (newSource != VideoSourceType.FileVideo)
                {
                    CurrentVideoPath = string.Empty;
                }

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
                using (var frameClone = frame.Clone())
                {
                    // Быстрое обновление UI с кадром
                    BitmapSource bitmapSource;
                    try
                    {
                        bitmapSource = Converters.BitmapSourceConverter.ToBitmapSourceAlternative(frameClone);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Primary conversion failed, using alternative method");
                        bitmapSource = Converters.BitmapSourceConverter.ToBitmapSourceAlternative(frameClone);
                    }

                    await _dispatcher.InvokeAsync(() =>
                    {
                        CurrentFrame = bitmapSource;
                        TotalFramesProcessed++; // Счетчик кадров в UI потоке
                    });

                    // Асинхронная обработка ML модели без блокировки видео потока
                    _ = ProcessFrameAsync(frameClone.Clone());
                }
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "Frame already disposed, skipping processing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing frame");
            }
        }

        private async Task ProcessFrameAsync(Mat frame)
        {
            try
            {
                using (frame)
                {
                    var prediction = _modelService.Predict(frame);

                    // Обновляем счетчик дефектов через Dispatcher
                    if (prediction.DefectType != DefectType.None &&
                        prediction.Confidence >= _config.ModelSettings.ConfidenceThreshold)
                    {
                        await _dispatcher.InvokeAsync(() =>
                        {
                            DefectCount++;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in async frame processing");
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
                        Location = new System.Drawing.Rectangle(0, 0, 0, 0)
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
            if (_isSelectingFile) return;

            try
            {
                StatusMessage = $"Connecting to {SelectedVideoSource}...";

                // Для файлового видео: если файл уже выбран, используем его
                // Если не выбран - открываем диалог
                if (SelectedVideoSource == VideoSourceType.FileVideo)
                {
                    var fileVideoService = _videoService as FileVideoService;
                    if (fileVideoService != null && string.IsNullOrEmpty(CurrentVideoPath))
                    {
                        // Файл не выбран, открываем диалог
                        await SelectVideoFileAsync();
                        if (string.IsNullOrEmpty(CurrentVideoPath))
                        {
                            return; // Пользователь отменил выбор файла
                        }
                    }
                }

                // Подключаемся к видео источнику
                bool connected = await _videoService.ConnectAsync();
                if (!connected)
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

                // Create subdirectory for defect type
                var defectTypeDir = Path.Combine(trainingDir, CurrentDefect?.DefectType.ToString() ?? "None");
                if (!Directory.Exists(defectTypeDir))
                {
                    Directory.CreateDirectory(defectTypeDir);
                }

                // Generate filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = Path.Combine(defectTypeDir, $"{timestamp}.jpg");

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

        private void OpenTrainingDialog()
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    var trainingDialog = _serviceProvider.GetService<TrainingDialog>();
                    if (trainingDialog != null)
                    {
                        // Получаем главное окно через Dispatcher
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        trainingDialog.Owner = mainWindow;
                        trainingDialog.ShowDialog();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open training dialog");
                StatusMessage = "Error opening training dialog";
            }
        }

        private void ResetCounters()
        {
            DefectCount = 0;
            TotalFramesProcessed = 0;
            StatusMessage = "Counters reset";
            _logger.LogInformation("Counters reset");
        }

        public async Task InitializeAsync()
        {
            try
            {
                StatusMessage = "Initializing application...";

                // Предварительное подключение к видео источнику (кроме файлового)
                if (SelectedVideoSource != VideoSourceType.FileVideo)
                {
                    // ИСПРАВЛЕНО: добавлен параметр null
                    //if (await _videoService.ConnectAsync(null))
                    //{
                    //    StatusMessage = $"{SelectedVideoSource} connected. Ready to start monitoring.";
                    //}
                    //else
                    //{
                    //    StatusMessage = $"Failed to connect to {SelectedVideoSource}. Please check settings.";
                    //}
                }
                else
                {
                    StatusMessage = "Select a video file to start monitoring";
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