using CardboardQualityControl.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Vision;
using System.IO;
using System.Text.Json;

namespace CardboardQualityControl.ML
{
    public class TrainingService
    {
        private readonly ILogger<TrainingService> _logger;
        private readonly TrainingSettings _settings;
        private MLContext _mlContext;
        private readonly ModelSettings _modelSettings;

        public TrainingService(ILogger<TrainingService> logger, TrainingSettings settings, ModelSettings modelSettings)
        {
            _logger = logger;
            _settings = settings;
            _modelSettings = modelSettings; // Сохраняем modelSettings
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

                // Prepare data
                var data = _mlContext.Data.LoadFromEnumerable(
                    trainingImages.Select(img => new ModelInput
                    {
                        ImagePath = img.ImagePath,
                        Label = img.DefectType.ToString()
                    }));

                // Define pipeline
                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label")
     .Append(_mlContext.Transforms.LoadRawImageBytes("Image", null, "ImagePath"))
     .Append(_mlContext.Transforms.ResizeImages("Image", _modelSettings.InputWidth, _modelSettings.InputHeight, "Image"))
     .Append(_mlContext.Transforms.ExtractPixels("Input", "Image", interleavePixelColors: true,
         offsetImage: 117, scaleImage: 1 / 117f))
     .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(
         new ImageClassificationTrainer.Options
         {
             FeatureColumnName = "Input",
             LabelColumnName = "Label",
             Arch = ImageClassificationTrainer.Architecture.ResnetV250,
             Epoch = _settings.Epochs,
             BatchSize = _settings.BatchSize,
             MetricsCallback = LogMetrics
         }));

                // Train model
                var model = pipeline.Fit(data);

                // Save model
                using var stream = new FileStream(outputModelPath, FileMode.Create, FileAccess.Write, FileShare.Write);
                _mlContext.Model.Save(model, data.Schema, stream);

                // Save class labels
                var classLabels = trainingImages.Select(img => img.DefectType.ToString()).Distinct().ToArray();
                var labelsPath = Path.Combine(Path.GetDirectoryName(outputModelPath)!, "class_labels.json");
                var json = JsonSerializer.Serialize(classLabels);
                File.WriteAllText(labelsPath, json);

                _logger.LogInformation("Model training completed and saved to {Path}", outputModelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to train model");
                return false;
            }
        }

        private void LogMetrics(ImageClassificationTrainer.ImageClassificationMetrics metrics)
        {
            // Просто логируем всю информацию о метриках
            _logger.LogInformation("Training metrics: {Metrics}", metrics);
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

                // Загружаем существующую модель
                var existingModel = _mlContext.Model.Load(existingModelPath, out var modelInputSchema);

                // Подготавливаем новые данные
                var newData = _mlContext.Data.LoadFromEnumerable(
                    newImages.Select(img => new ModelInput
                    {
                        ImagePath = img.ImagePath,
                        Label = img.DefectType.ToString()
                    }));

                // Для переобучения нужно создать новый конвейер, а не использовать Fit у существующей модели
                var retrainingPipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label")
                    .Append(_mlContext.Transforms.LoadRawImageBytes("Image", null, "ImagePath"))
                    .Append(_mlContext.Transforms.ResizeImages("Image", _modelSettings.InputWidth, _modelSettings.InputHeight, "Image"))
                    .Append(_mlContext.Transforms.ExtractPixels("Input", "Image", interleavePixelColors: true,
                        offsetImage: 117, scaleImage: 1 / 117f))
                    .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(
                        new ImageClassificationTrainer.Options
                        {
                            FeatureColumnName = "Input",
                            LabelColumnName = "Label",
                            Arch = ImageClassificationTrainer.Architecture.ResnetV250,
                            Epoch = _settings.Epochs,
                            BatchSize = _settings.BatchSize
                        }));

                // Обучаем новую модель на комбинированных данных
                // В реальном сценарии вам可能需要 объединить старые и новые данные
                var retrainedModel = retrainingPipeline.Fit(newData);

                // Сохраняем переобученную модель
                using var stream = new FileStream(outputModelPath, FileMode.Create, FileAccess.Write, FileShare.Write);
                _mlContext.Model.Save(retrainedModel, newData.Schema, stream);

                _logger.LogInformation("Model retraining completed and saved to {Path}", outputModelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrain model");
                return false;
            }
        }
    }
}