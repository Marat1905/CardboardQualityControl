using Basler.Pylon;
using OpenCvSharp;
using Microsoft.Extensions.Logging;
using CardboardQualityControl.Models;

namespace CardboardQualityControl.Services
{
    public class BaslerVideoService : IVideoService
    {
        private readonly ILogger<BaslerVideoService> _logger;
        private readonly BaslerCameraSettings _settings;
        private Camera? _camera;
        private bool _isCapturing;

        public event Action<Mat>? FrameReady;
        public bool IsConnected => _camera?.IsConnected == true;

        public BaslerVideoService(ILogger<BaslerVideoService> logger, BaslerCameraSettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.LogInformation("Connecting to Basler camera...");

                // Find and create camera
                var cameraInfo = CameraFinder.Enumerate().FirstOrDefault();
                if (cameraInfo == null)
                {
                    _logger.LogError("No Basler camera found");
                    return false;
                }

                _camera = new Camera(cameraInfo);
                _camera.Open();

                // Configure camera settings
                _camera.Parameters[PLCamera.ExposureTime].SetValue(_settings.ExposureTime);
                _camera.Parameters[PLCamera.Gain].SetValue(_settings.Gain);

                _logger.LogInformation("Basler camera connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Basler camera");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            await StopCaptureAsync();
            _camera?.Close();
            _camera?.Dispose();
            _camera = null;
            _logger.LogInformation("Basler camera disconnected");
        }

        public async Task StartCaptureAsync()
        {
            if (_camera == null || !_camera.IsConnected)
            {
                _logger.LogWarning("Camera is not connected");
                return;
            }

            if (_isCapturing) return;

            try
            {
                _camera.StreamGrabber.Start();
                _camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;
                _isCapturing = true;
                _logger.LogInformation("Started capturing from Basler camera");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start capturing from Basler camera");
            }
        }

        public async Task StopCaptureAsync()
        {
            if (_camera == null || !_isCapturing) return;

            try
            {
                _camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;
                _camera.StreamGrabber.Stop();
                _isCapturing = false;
                _logger.LogInformation("Stopped capturing from Basler camera");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop capturing from Basler camera");
            }
        }

        private void OnImageGrabbed(object sender, ImageGrabbedEventArgs e)
        {
            try
            {
                using (var grabResult = e.GrabResult)
                {
                    if (!grabResult.IsValid) return;

                    // Convert grab result to OpenCV Mat
                    using (var converter = new PixelDataConverter())
                    {
                        // Вычисляем размер буфера
                        int bufferSize = grabResult.Width * grabResult.Height * 3; // 3 канала для BGR
                        var buffer = new byte[bufferSize];

                        converter.OutputPixelFormat = PixelType.BGR8packed;
                        converter.Convert(buffer, grabResult);

                        // Используем рекомендуемый метод FromPixelData вместо устаревшего конструктора
                        using (var mat = Mat.FromPixelData(grabResult.Height, grabResult.Width, MatType.CV_8UC3, buffer))
                        {
                            FrameReady?.Invoke(mat.Clone());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Basler camera frame");
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _camera?.Dispose();
        }
    }
}