using CardboardQualityControl.Models;
using CardboardQualityControl.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CardboardQualityControl.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly AppConfig _config;
        private readonly string _configFilePath;

        public AppConfig Config { get; }

        public ObservableCollection<KeyValuePair<string, string>> VideoSources { get; } = new()
        {
            new("Basler", "Basler Camera"),
            new("IP", "IP Camera"),
            new("File", "Video File")
        };

        public ObservableCollection<string> PixelFormats { get; } = new()
        {
            "BGR8",
            "RGB8",
            "Mono8",
            "Mono12",
            "BayerRG8",
            "BayerRG12"
        };

        public ObservableCollection<string> VideoCodecs { get; } = new()
        {
            "MJPG",
            "XVID",
            "H264",
            "MP4V",
            "DIVX"
        };

        public SettingsViewModel(ILogger<SettingsViewModel> logger, AppConfig config)
        {
            _logger = logger;
            _config = config;
            Config = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config))!;
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        }

        public bool SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);

                // Update the main config
                _config.VideoSource = Config.VideoSource;
                _config.BaslerCameraSettings = Config.BaslerCameraSettings;
                _config.IpCameraSettings = Config.IpCameraSettings;
                _config.FileVideoSettings = Config.FileVideoSettings;
                _config.ModelSettings = Config.ModelSettings;
                _config.VideoRecordingSettings = Config.VideoRecordingSettings;

                _logger.LogInformation("Settings saved successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                return false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}