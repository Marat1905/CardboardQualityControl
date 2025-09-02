using Basler.Pylon;
using CardboardQualityControl.Models;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Text;

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
                _logger.LogInformation("Searching for Basler cameras...");

                // Проверка доступности камер
                var cameras = CameraFinder.Enumerate().ToList();
                if (cameras.Count == 0)
                {
                    _logger.LogWarning("No Basler cameras found");
                    return false;
                }

                _logger.LogInformation($"Found {cameras.Count} camera(s)");

                // Выбор камеры
                var cameraInfo = cameras.First();
                _camera = new Camera(cameraInfo);

                // ОТЛАДКА: Вывод информации о камере
                _logger.LogInformation($"Camera Model: {cameraInfo[CameraInfoKey.ModelName]}");
                _logger.LogInformation($"Camera Serial: {cameraInfo[CameraInfoKey.SerialNumber]}");
                _logger.LogInformation($"Camera Vendor: {cameraInfo[CameraInfoKey.VendorName]}");

                // Открываем камеру
                _camera.Open();

                // ОТЛАДКА: Проверка состояния камеры
                _logger.LogInformation($"Camera connected: {_camera.IsConnected}");
                _logger.LogInformation($"Camera opened: {_camera.IsOpen}");

                // Настройка параметров камеры
                ConfigureCameraSettings();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Basler camera");
                return false;
            }
        }

        private void ConfigureCameraSettings()
        {
            if (_camera == null) return;

            try
            {
                // Сбрасываем настройки к default
                _camera.Parameters[PLCamera.UserSetSelector].SetValue(PLCamera.UserSetSelector.Default);
                _camera.Parameters[PLCamera.UserSetLoad].Execute();

                // Базовые настройки
                _camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);

                // Автоматические настройки для начала
                _camera.Parameters[PLCamera.ExposureAuto].SetValue(PLCamera.ExposureAuto.Once);
                _camera.Parameters[PLCamera.GainAuto].SetValue(PLCamera.GainAuto.Once);
                _camera.Parameters[PLCamera.BalanceWhiteAuto].SetValue(PLCamera.BalanceWhiteAuto.Once);

                // Формат пикселей
                _camera.Parameters[PLCamera.PixelFormat].SetValue(PLCamera.PixelFormat.BGR8);

                // Настройка размера изображения (максимальный)
                _camera.Parameters[PLCamera.Width].SetValue(_camera.Parameters[PLCamera.Width].GetMaximum());
                _camera.Parameters[PLCamera.Height].SetValue(_camera.Parameters[PLCamera.Height].GetMaximum());

                _logger.LogInformation("Camera configured successfully");

                // ОТЛАДКА: Вывод текущих параметров
                _logger.LogInformation($"Width: {_camera.Parameters[PLCamera.Width].GetValue()}");
                _logger.LogInformation($"Height: {_camera.Parameters[PLCamera.Height].GetValue()}");
                _logger.LogInformation($"PixelFormat: {_camera.Parameters[PLCamera.PixelFormat].GetValue()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring camera settings");
            }
        }

       

        public async Task DisconnectAsync()
        {
            await StopCaptureAsync();

            try
            {
                if (_camera != null)
                {
                    if (_camera.StreamGrabber.IsGrabbing)
                    {
                        _camera.StreamGrabber.Stop();
                    }

                    _camera.Close();
                    _camera.Dispose();
                    _camera = null;
                }
                _logger.LogInformation("Basler camera disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during camera disconnection");
            }
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
                // Останавливаем grabber если уже запущен
                if (_camera.StreamGrabber.IsGrabbing)
                {
                    _camera.StreamGrabber.Stop();
                }

                // Подписываемся на события
                _camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;
                _camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;

                // Запускаем захват
                _camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                _isCapturing = true;

                _logger.LogInformation("Capture started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start capture");
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
                _logger.LogInformation("Stopped video capture");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop video capture");
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
            try
            {
                DisconnectAsync().Wait();
                _camera?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during camera disposal");
            }
        }

        public static string GetPylonVersionInfo()
        {
            try
            {
                var assembly = typeof(Camera).Assembly;
                return $"Basler Pylon .NET SDK Version: {assembly.GetName().Version}";
            }
            catch
            {
                return "Unable to determine Pylon version";
            }
        }

        public Task<bool> ConnectAsync(string? filePath = null)
        {
            throw new NotImplementedException();
        }
    }
}