using CardboardQualityControl.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.IO;
using System.Text.Json;

namespace CardboardQualityControl.ML
{
    public class TrainingService
    {
        private readonly ILogger<TrainingService> _logger;
        private readonly TrainingSettings _settings;
        private readonly ModelSettings _modelSettings;
        private MLContext _mlContext;

        public TrainingService(ILogger<TrainingService> logger,
                              TrainingSettings settings,
                              ModelSettings modelSettings)
        {
            _logger = logger;
            _settings = settings;
            _modelSettings = modelSettings;
            _mlContext = new MLContext(seed: 0);
        }

        public bool TrainModel(IEnumerable<TrainingImage> trainingImages, string outputModelPath)
        {
            try
            {
                _logger.LogInformation("Starting model training with {Count} images", trainingImages.Count());

                if (!trainingImages.Any())
                {
                    _logger.LogError("No training images provided");
                    return false;
                }

                // Подготовка данных
                var data = _mlContext.Data.LoadFromEnumerable(
                    trainingImages.Select(img => new ModelInput
                    {
                        ImagePath = img.ImagePath,
                        Label = img.DefectType.ToString()
                    }));

                // Разделение на тренировочную и тестовую выборки
                var trainTestSplit = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
                var trainData = trainTestSplit.TrainSet;
                var testData = trainTestSplit.TestSet;

                // Определение pipeline
                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "Label",
                    inputColumnName: "Label")

                .Append(_mlContext.Transforms.LoadRawImageBytes(
                    outputColumnName: "Image",
                    imageFolder: null,
                    inputColumnName: "ImagePath"))

                .Append(_mlContext.Transforms.ResizeImages(
                    outputColumnName: "ImageResized",
                    imageWidth: _modelSettings.InputWidth,
                    imageHeight: _modelSettings.InputHeight,
                    inputColumnName: "Image"))

                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "Features",
                    inputColumnName: "ImageResized",
                    interleavePixelColors: true,
                    offsetImage: 117f,
                    scaleImage: 1 / 117f))

                .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(
                    new Microsoft.ML.Vision.ImageClassificationTrainer.Options
                    {
                        FeatureColumnName = "Features",
                        LabelColumnName = "Label",
                        Arch = Microsoft.ML.Vision.ImageClassificationTrainer.Architecture.ResnetV250,
                        Epoch = _settings.Epochs,
                        BatchSize = _settings.BatchSize,
                        LearningRate = 0.01f,
                        ValidationSet = testData,
                        EarlyStoppingCriteria = new Microsoft.ML.Vision.ImageClassificationTrainer.EarlyStopping(
                            patience: 10,
                            minDelta: 0.001f)
                    }))

                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedLabel",
                    inputColumnName: "PredictedLabel"));

                // Обучение модели
                _logger.LogInformation("Training model...");
                var model = pipeline.Fit(trainData);

                // Валидация модели
                var predictions = model.Transform(testData);
                var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);

                _logger.LogInformation("Model training completed");
                _logger.LogInformation("Accuracy: {Accuracy}", metrics.MacroAccuracy);
                _logger.LogInformation("LogLoss: {LogLoss}", metrics.LogLoss);

                // Сохранение модели
                _mlContext.Model.Save(model, data.Schema, outputModelPath);

                // Сохранение метрик
                SaveTrainingMetrics(metrics, outputModelPath);

                // Сохранение class labels
                SaveClassLabels(trainingImages, outputModelPath);

                _logger.LogInformation("Model saved to: {Path}", outputModelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to train model");
                return false;
            }
        }

        private void SaveTrainingMetrics(MulticlassClassificationMetrics metrics, string modelPath)
        {
            try
            {
                var metricsData = new
                {
                    metrics.MacroAccuracy,
                    metrics.MicroAccuracy,
                    metrics.LogLoss,
                    metrics.PerClassLogLoss,
                    TrainingDate = DateTime.Now
                };

                var metricsJson = JsonSerializer.Serialize(metricsData, new JsonSerializerOptions { WriteIndented = true });
                var metricsPath = Path.Combine(Path.GetDirectoryName(modelPath), "training-metrics.json");
                File.WriteAllText(metricsPath, metricsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save training metrics");
            }
        }

        private void SaveClassLabels(IEnumerable<TrainingImage> trainingImages, string modelPath)
        {
            try
            {
                var classLabels = trainingImages
                    .Select(img => img.DefectType.ToString())
                    .Distinct()
                    .OrderBy(label => label)
                    .ToArray();

                var labelsJson = JsonSerializer.Serialize(classLabels);
                var labelsPath = Path.Combine(Path.GetDirectoryName(modelPath), "class-labels.json");
                File.WriteAllText(labelsPath, labelsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save class labels");
            }
        }

        public bool RetrainModel(IEnumerable<TrainingImage> newImages, string existingModelPath, string outputModelPath)
        {
            try
            {
                _logger.LogInformation("Starting model retraining with {Count} new images", newImages.Count());

                if (!File.Exists(existingModelPath))
                {
                    _logger.LogError("Existing model not found: {Path}", existingModelPath);
                    return false;
                }

                // Загрузка существующей модели
                var existingModel = _mlContext.Model.Load(existingModelPath, out var modelSchema);

                // Загрузка старых тренировочных данных (если доступны)
                var allTrainingImages = LoadAllTrainingImages().Concat(newImages);

                // Полное переобучение на всех данных
                return TrainModel(allTrainingImages, outputModelPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrain model");
                return false;
            }
        }

        private IEnumerable<TrainingImage> LoadAllTrainingImages()
        {
            var trainingDir = _settings.TrainingDataPath;
            if (!Directory.Exists(trainingDir))
                return Enumerable.Empty<TrainingImage>();

            var images = new List<TrainingImage>();

            foreach (var subDir in Directory.GetDirectories(trainingDir))
            {
                var defectTypeName = Path.GetFileName(subDir);
                if (Enum.TryParse<DefectType>(defectTypeName, out var defectType))
                {
                    var imageFiles = Directory.GetFiles(subDir, "*.jpg")
                        .Concat(Directory.GetFiles(subDir, "*.png"))
                        .Concat(Directory.GetFiles(subDir, "*.bmp"));

                    images.AddRange(imageFiles.Select(file => new TrainingImage
                    {
                        ImagePath = file,
                        DefectType = defectType
                    }));
                }
            }

            return images;
        }

        public IEnumerable<string> GetSupportedImageFormats()
        {
            return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };
        }

        public (int totalImages, Dictionary<DefectType, int> countByClass) GetTrainingDataStats()
        {
            var images = LoadAllTrainingImages().ToList();
            var countByClass = images.GroupBy(img => img.DefectType)
                .ToDictionary(g => g.Key, g => g.Count());

            return (images.Count, countByClass);
        }
    }
}