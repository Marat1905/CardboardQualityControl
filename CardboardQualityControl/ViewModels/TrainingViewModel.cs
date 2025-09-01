using CardboardQualityControl.ML;
using CardboardQualityControl.Models;
using CardboardQualityControl.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CardboardQualityControl.ViewModels
{
    public class TrainingViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<TrainingViewModel> _logger;
        private readonly TrainingService _trainingService;
        private readonly AppConfig _config;

        private ObservableCollection<TrainingImage> _trainingImages = new();
        private bool _isTraining;
        private string _trainingStatus = "Ready";
        private float _trainingProgress;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<TrainingImage> TrainingImages
        {
            get => _trainingImages;
            set
            {
                _trainingImages = value;
                OnPropertyChanged();
            }
        }

        public bool IsTraining
        {
            get => _isTraining;
            set
            {
                _isTraining = value;
                OnPropertyChanged();
            }
        }

        public string TrainingStatus
        {
            get => _trainingStatus;
            set
            {
                _trainingStatus = value;
                OnPropertyChanged();
            }
        }

        public float TrainingProgress
        {
            get => _trainingProgress;
            set
            {
                _trainingProgress = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadTrainingImagesCommand { get; }
        public ICommand StartTrainingCommand { get; }
        public ICommand StartRetrainingCommand { get; }

        public TrainingViewModel(ILogger<TrainingViewModel> logger, TrainingService trainingService, AppConfig config)
        {
            _logger = logger;
            _trainingService = trainingService;
            _config = config;

            LoadTrainingImagesCommand = new RelayCommand(_ => LoadTrainingImages());
            StartTrainingCommand = new RelayCommand(async _ => await StartTrainingAsync());
            StartRetrainingCommand = new RelayCommand(async _ => await StartRetrainingAsync());
        }

        private void LoadTrainingImages()
        {
            try
            {
                TrainingImages.Clear();

                var trainingDir = _config.TrainingSettings.TrainingDataPath;
                if (!Directory.Exists(trainingDir))
                {
                    TrainingStatus = "Training directory does not exist";
                    return;
                }

                var imageFiles = Directory.GetFiles(trainingDir, "*.jpg")
                    .Concat(Directory.GetFiles(trainingDir, "*.png"))
                    .ToArray();

                foreach (var file in imageFiles)
                {
                    // Извлекаем тип дефекта из имени файла (формат: DefectType_timestamp.jpg)
                    var filename = Path.GetFileNameWithoutExtension(file);
                    var parts = filename.Split('_');
                    var defectType = DefectType.None;  // Используем None вместо Unknown

                    if (parts.Length > 0 && Enum.TryParse(parts[0], out DefectType parsedType))
                    {
                        defectType = parsedType;
                    }

                    TrainingImages.Add(new TrainingImage
                    {
                        ImagePath = file,
                        DefectType = defectType
                    });
                }

                TrainingStatus = $"Loaded {TrainingImages.Count} training images";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load training images");
                TrainingStatus = "Error loading training images";
            }
        }

        private async Task StartTrainingAsync()
        {
            if (!TrainingImages.Any())
            {
                TrainingStatus = "No training images available";
                return;
            }

            try
            {
                IsTraining = true;
                TrainingStatus = "Starting training...";
                TrainingProgress = 0;

                // Run training in background thread
                await Task.Run(() =>
                {
                    var outputPath = Path.Combine(Path.GetDirectoryName(_config.ModelSettings.ModelPath)!,
                                                $"model_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

                    var success = _trainingService.TrainModel(TrainingImages, outputPath);

                    if (success)
                    {
                        TrainingStatus = $"Training completed. Model saved to: {outputPath}";
                    }
                    else
                    {
                        TrainingStatus = "Training failed";
                    }
                });

                TrainingProgress = 100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Training failed");
                TrainingStatus = "Training failed with error";
            }
            finally
            {
                IsTraining = false;
            }
        }

        private async Task StartRetrainingAsync()
        {
            if (!TrainingImages.Any())
            {
                TrainingStatus = "No training images available";
                return;
            }

            if (!File.Exists(_config.ModelSettings.ModelPath))
            {
                TrainingStatus = "Existing model not found for retraining";
                return;
            }

            try
            {
                IsTraining = true;
                TrainingStatus = "Starting retraining...";
                TrainingProgress = 0;

                // Run retraining in background thread
                await Task.Run(() =>
                {
                    var outputPath = Path.Combine(Path.GetDirectoryName(_config.ModelSettings.ModelPath)!,
                                                $"model_retrained_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

                    var success = _trainingService.RetrainModel(TrainingImages, _config.ModelSettings.ModelPath, outputPath);

                    if (success)
                    {
                        TrainingStatus = $"Retraining completed. Model saved to: {outputPath}";
                    }
                    else
                    {
                        TrainingStatus = "Retraining failed";
                    }
                });

                TrainingProgress = 100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retraining failed");
                TrainingStatus = "Retraining failed with error";
            }
            finally
            {
                IsTraining = false;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}