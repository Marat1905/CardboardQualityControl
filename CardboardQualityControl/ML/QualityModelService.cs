using CardboardQualityControl.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using OpenCvSharp;
using System.IO;
using System.Text.Json;

namespace CardboardQualityControl.ML
{
    public class QualityModelService
    {
        private readonly ILogger<QualityModelService> _logger;
        private readonly ModelSettings _settings;
        private MLContext _mlContext;
        private ITransformer? _model;
        private PredictionEngine<ModelInput, ModelOutput>? _predictor;
        private string[] _classLabels = Array.Empty<string>();

        public event Action<ModelOutput>? PredictionReady;

        public QualityModelService(ILogger<QualityModelService> logger, ModelSettings settings)
        {
            _logger = logger;
            _settings = settings;
            _mlContext = new MLContext(seed: 0);
        }

        public bool LoadModel()
        {
            try
            {
                if (!File.Exists(_settings.ModelPath))
                {
                    _logger.LogError("Model file does not exist: {ModelPath}", _settings.ModelPath);
                    return false;
                }

                _model = _mlContext.Model.Load(_settings.ModelPath, out _);
                _predictor = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);

                // Load class labels from model or default
                LoadClassLabels();

                _logger.LogInformation("Model loaded successfully from {ModelPath}", _settings.ModelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load model from {ModelPath}", _settings.ModelPath);
                return false;
            }
        }

        private void LoadClassLabels()
        {
            try
            {
                var labelsPath = Path.Combine(Path.GetDirectoryName(_settings.ModelPath)!, "class_labels.json");
                if (File.Exists(labelsPath))
                {
                    var json = File.ReadAllText(labelsPath);
                    _classLabels = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
                }
                else
                {
                    // Default labels if not found
                    _classLabels = Enum.GetNames(typeof(DefectType));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load class labels");
                _classLabels = Enum.GetNames(typeof(DefectType));
            }
        }

        public ModelOutput Predict(Mat image)
        {
            if (_predictor == null)
            {
                _logger.LogWarning("Model is not loaded");
                return new ModelOutput { DefectType = DefectType.None, Confidence = 0 };
            }

            try
            {
                // Preprocess image
                using var resized = new Mat();
                Cv2.Resize(image, resized, new Size(_settings.InputWidth, _settings.InputHeight));

                // Convert to byte array (RGB format)
                var imageBytes = resized.ToBytes(".jpg");

                // Create input
                var input = new ModelInput { Image = imageBytes };

                // Make prediction
                var prediction = _predictor.Predict(input);

                // Post-process prediction
                prediction.DefectType = GetDefectTypeFromPrediction(prediction);
                prediction.Confidence = GetConfidence(prediction);

                PredictionReady?.Invoke(prediction);
                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to make prediction");
                return new ModelOutput { DefectType = DefectType.None, Confidence = 0 };
            }
        }

        private DefectType GetDefectTypeFromPrediction(ModelOutput prediction)
        {
            if (prediction.Probabilities == null || prediction.Probabilities.Length == 0)
                return DefectType.None;

            // Find the index with highest probability
            var maxIndex = 0;
            var maxValue = prediction.Probabilities[0];

            for (int i = 1; i < prediction.Probabilities.Length; i++)
            {
                if (prediction.Probabilities[i] > maxValue)
                {
                    maxValue = prediction.Probabilities[i];
                    maxIndex = i;
                }
            }

            // Convert index to DefectType
            if (maxIndex < _classLabels.Length &&
                Enum.TryParse(_classLabels[maxIndex], out DefectType defectType))
            {
                return defectType;
            }

            return (DefectType)maxIndex;
        }

        private float GetConfidence(ModelOutput prediction)
        {
            if (prediction.Probabilities == null || prediction.Probabilities.Length == 0)
                return 0;

            // Return the highest probability
            return prediction.Probabilities.Max();
        }

        public void SaveModel(string path)
        {
            if (_model == null)
            {
                _logger.LogWarning("No model to save");
                return;
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write);
                _mlContext.Model.Save(_model, null, stream);

                // Save class labels
                var labelsPath = Path.Combine(Path.GetDirectoryName(path)!, "class_labels.json");
                var json = JsonSerializer.Serialize(_classLabels);
                File.WriteAllText(labelsPath, json);

                _logger.LogInformation("Model saved to {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save model to {Path}", path);
            }
        }
    }
}