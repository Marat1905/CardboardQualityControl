using Basler.Pylon;
using CardboardQualityControl.Models;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CardboardQualityControl.Services
{
    public class BaslerVideoService : IVideoService
    {
        private readonly ILogger<BaslerVideoService> _logger;
        private readonly BaslerCameraSettings _settings;
        private Camera? _camera;
        private bool _isCapturing;
        private OpenCvSharp.VideoWriter? _videoWriter;
        private string? _currentRecordingPath;
        private Queue<Mat> _preRecordBuffer;
        private readonly int _preRecordBufferSize;
        private double _fps;
        private double _currentPosition;
        private double _totalFrames;
        private DateTime _lastFrameTime;
        private int _frameCount;

        public event Action<Mat>? FrameReady;
        public bool IsConnected => _camera?.IsConnected == true;
        public double FPS => _fps;
        public double CurrentPosition => _currentPosition;
        public double TotalFrames => _totalFrames;
        public bool IsRecording => _videoWriter != null && _videoWriter.IsOpened();

        public BaslerCameraSettings CameraSettings => _settings;

        public BaslerVideoService(ILogger<BaslerVideoService> logger, BaslerCameraSettings settings)
        {
            _logger = logger;
            _settings = settings;
            _preRecordBuffer = new Queue<Mat>();
            _preRecordBufferSize = 5 * 30; // 5 seconds at 30 FPS
            _fps = settings.FPS;
            _lastFrameTime = DateTime.Now;
        }

        public async Task<bool> ConnectAsync()
        {
            return await ConnectAsync(null);
        }

        public async Task<bool> ConnectAsync(string? filePath = null)
        {
            try
            {
                _logger.LogInformation("Searching for Basler cameras...");

                var cameras = CameraFinder.Enumerate().ToList();
                if (cameras.Count == 0)
                {
                    _logger.LogWarning("No Basler cameras found");
                    return false;
                }

                _logger.LogInformation($"Found {cameras.Count} camera(s)");

                var cameraInfo = cameras.First();
                _camera = new Camera(cameraInfo);

                _logger.LogInformation($"Camera Model: {cameraInfo[CameraInfoKey.ModelName]}");
                _logger.LogInformation($"Camera Serial: {cameraInfo[CameraInfoKey.SerialNumber]}");
                _logger.LogInformation($"Camera Vendor: {cameraInfo[CameraInfoKey.VendorName]}");

                _camera.Open();

                _logger.LogInformation($"Camera connected: {_camera.IsConnected}");
                _logger.LogInformation($"Camera opened: {_camera.IsOpen}");

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
                // Reset to default
                SetEnumParameter("UserSetSelector", "Default");
                ExecuteCommand("UserSetLoad");

                // Basic settings
                SetEnumParameter("AcquisitionMode", "Continuous");

                // Apply auto settings
                if (_settings.AutoExposure)
                {
                    SetEnumParameter("ExposureAuto", "Continuous");
                }
                else
                {
                    SetEnumParameter("ExposureAuto", "Off");
                    SetFloatParameter("ExposureTime", _settings.ExposureTime);
                }

                if (_settings.AutoGain)
                {
                    SetEnumParameter("GainAuto", "Continuous");
                }
                else
                {
                    SetEnumParameter("GainAuto", "Off");
                    SetFloatParameter("Gain", _settings.Gain);
                }

                if (_settings.AutoWhiteBalance)
                {
                    SetEnumParameter("BalanceWhiteAuto", "Continuous");
                }
                else
                {
                    SetEnumParameter("BalanceWhiteAuto", "Off");
                }

                // Set pixel format
                SetEnumParameter("PixelFormat", _settings.PixelFormat);

                // Set resolution
                SetIntegerParameter("Width", _settings.Width);
                SetIntegerParameter("Height", _settings.Height);

                // Set FPS
                SetBooleanParameter("AcquisitionFrameRateEnable", true);
                SetFloatParameter("AcquisitionFrameRate", _settings.FPS);

                // Try to set additional parameters if available
                try
                {
                    if (_camera.Parameters.Contains("Gamma"))
                    {
                        SetFloatParameter("Gamma", _settings.Gamma);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gamma parameter not available");
                }

                try
                {
                    // Try to set brightness if available
                    var brightnessParam = _camera.Parameters.FirstOrDefault(p => p.Name.Contains("Brightness", StringComparison.OrdinalIgnoreCase));
                    if (brightnessParam != null)
                    {
                        SetFloatParameter(brightnessParam.Name, _settings.Brightness);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Brightness parameter not available");
                }

                try
                {
                    // Try to set contrast if available
                    var contrastParam = _camera.Parameters.FirstOrDefault(p => p.Name.Contains("Contrast", StringComparison.OrdinalIgnoreCase));
                    if (contrastParam != null)
                    {
                        SetFloatParameter(contrastParam.Name, _settings.Contrast);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Contrast parameter not available");
                }

                _logger.LogInformation("Camera configured successfully");
                _logger.LogInformation($"Width: {GetIntegerParameter("Width")}");
                _logger.LogInformation($"Height: {GetIntegerParameter("Height")}");
                _logger.LogInformation($"PixelFormat: {GetEnumParameter("PixelFormat")}");
                _logger.LogInformation($"FPS: {GetFloatParameter("AcquisitionFrameRate")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring camera settings");
            }
        }

        private void SetEnumParameter(string parameterName, string value)
        {
            try
            {
                var parameter = _camera?.Parameters[parameterName] as IEnumParameter;
                if (parameter != null && parameter.IsWritable)
                {
                    parameter.SetValue(value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to set enum parameter {parameterName} to {value}");
            }
        }

        private void SetFloatParameter(string parameterName, double value)
        {
            try
            {
                var parameter = _camera?.Parameters[parameterName] as IFloatParameter;
                if (parameter != null && parameter.IsWritable)
                {
                    parameter.SetValue(value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to set float parameter {parameterName} to {value}");
            }
        }

        private void SetIntegerParameter(string parameterName, long value)
        {
            try
            {
                var parameter = _camera?.Parameters[parameterName] as IIntegerParameter;
                if (parameter != null && parameter.IsWritable)
                {
                    parameter.SetValue(value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to set integer parameter {parameterName} to {value}");
            }
        }

        private void SetBooleanParameter(string parameterName, bool value)
        {
            try
            {
                var parameter = _camera?.Parameters[parameterName] as IBooleanParameter;
                if (parameter != null && parameter.IsWritable)
                {
                    parameter.SetValue(value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to set boolean parameter {parameterName} to {value}");
            }
        }

        private void ExecuteCommand(string commandName)
        {
            try
            {
                var parameter = _camera?.Parameters[commandName] as ICommandParameter;
                parameter?.Execute();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to execute command {commandName}");
            }
        }

        private string GetEnumParameter(string parameterName)
        {
            try
            {
                var parameter = _camera?.Parameters[parameterName] as IEnumParameter;
                return parameter?.GetValue() ?? "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        private double GetFloatParameter(string parameterName)
        {
            try
            {
                var parameter = _camera?.Parameters[parameterName] as IFloatParameter;
                return parameter?.GetValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private long GetIntegerParameter(string parameterName)
        {
            try
            {
                var parameter = _camera?.Parameters[parameterName] as IIntegerParameter;
                return parameter?.GetValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task DisconnectAsync()
        {
            await StopRecordingAsync();
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
                if (_camera.StreamGrabber.IsGrabbing)
                {
                    _camera.StreamGrabber.Stop();
                }

                _camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;
                _camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;

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

                    // Calculate FPS
                    var currentTime = DateTime.Now;
                    var elapsed = (currentTime - _lastFrameTime).TotalSeconds;
                    _frameCount++;

                    if (elapsed >= 1.0) // Update FPS every second
                    {
                        _fps = _frameCount / elapsed;
                        _frameCount = 0;
                        _lastFrameTime = currentTime;
                    }

                    using (var converter = new PixelDataConverter())
                    {
                        int bufferSize = grabResult.Width * grabResult.Height * 3;
                        var buffer = new byte[bufferSize];

                        converter.OutputPixelFormat = PixelType.BGR8packed;
                        converter.Convert(buffer, grabResult);

                        using (var mat = Mat.FromPixelData(grabResult.Height, grabResult.Width, MatType.CV_8UC3, buffer))
                        {
                            _currentPosition++;

                            // Add to pre-record buffer
                            if (IsRecording)
                            {
                                _preRecordBuffer.Enqueue(mat.Clone());
                                if (_preRecordBuffer.Count > _preRecordBufferSize)
                                {
                                    var oldFrame = _preRecordBuffer.Dequeue();
                                    oldFrame?.Dispose();
                                }
                            }

                            FrameReady?.Invoke(mat.Clone());

                            // Write to video file if recording
                            if (IsRecording && _videoWriter != null)
                            {
                                _videoWriter.Write(mat);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Basler camera frame");
            }
        }

        public async Task StartRecordingAsync(string outputPath)
        {
            if (_camera == null || !_camera.IsConnected) return;

            try
            {
                await StopRecordingAsync();

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var frameSize = new OpenCvSharp.Size(_settings.Width, _settings.Height);

                // Use MJPG codec for compatibility
                int fourcc = OpenCvSharp.VideoWriter.FourCC('M', 'J', 'P', 'G');

                _videoWriter = new OpenCvSharp.VideoWriter(
                    outputPath,
                    fourcc,
                    _settings.FPS,
                    frameSize,
                    true
                );

                if (_videoWriter.IsOpened())
                {
                    _currentRecordingPath = outputPath;
                    _logger.LogInformation($"Started recording to: {outputPath}");

                    // Write pre-record buffer
                    foreach (var frame in _preRecordBuffer)
                    {
                        _videoWriter.Write(frame);
                    }
                }
                else
                {
                    _logger.LogError("Failed to open video writer");
                    _videoWriter?.Dispose();
                    _videoWriter = null;
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

                    // Clear pre-record buffer
                    foreach (var frame in _preRecordBuffer)
                    {
                        frame.Dispose();
                    }
                    _preRecordBuffer.Clear();

                    _logger.LogInformation($"Stopped recording: {_currentRecordingPath}");
                    _currentRecordingPath = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping recording");
                }
            }
        }

        public Task SeekAsync(double position)
        {
            // Not implemented for live camera
            return Task.CompletedTask;
        }

        public void UpdateCameraSettings(BaslerCameraSettings newSettings)
        {
            _settings.AutoExposure = newSettings.AutoExposure;
            _settings.AutoGain = newSettings.AutoGain;
            _settings.AutoWhiteBalance = newSettings.AutoWhiteBalance;
            _settings.ExposureTime = newSettings.ExposureTime;
            _settings.Gain = newSettings.Gain;
            _settings.PixelFormat = newSettings.PixelFormat;
            _settings.Width = newSettings.Width;
            _settings.Height = newSettings.Height;
            _settings.FPS = newSettings.FPS;
            _settings.Gamma = newSettings.Gamma;
            _settings.Brightness = newSettings.Brightness;
            _settings.Contrast = newSettings.Contrast;

            if (_camera != null && _camera.IsConnected)
            {
                ConfigureCameraSettings();
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

                _camera?.Dispose();

                foreach (var frame in _preRecordBuffer)
                {
                    frame.Dispose();
                }
                _preRecordBuffer.Clear();
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
    }
}