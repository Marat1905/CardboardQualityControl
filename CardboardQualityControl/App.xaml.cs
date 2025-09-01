using CardboardQualityControl.ML;
using CardboardQualityControl.Models;
using CardboardQualityControl.Services;
using CardboardQualityControl.ViewModels;
using CardboardQualityControl.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CardboardQualityControl
{
    public partial class App : Application
    {
        private IHost? _host;

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Configuration
                    var config = LoadConfiguration();
                    services.AddSingleton(config);

                    // Регистрируем отдельные настройки
                    services.AddSingleton(config.BaslerCameraSettings);
                    services.AddSingleton(config.IpCameraSettings);
                    services.AddSingleton(config.FileVideoSettings);
                    services.AddSingleton(config.ModelSettings);
                    services.AddSingleton(config.TrainingSettings);

                    // Services
                    services.AddSingleton<VideoServiceFactory>();
                    services.AddSingleton<IVideoService>(provider =>
                    {
                        var factory = provider.GetRequiredService<VideoServiceFactory>();
                        return factory.CreateVideoService();
                    });
                    services.AddSingleton<System.Windows.Threading.Dispatcher>(provider =>
    Application.Current.Dispatcher);
                    services.AddSingleton<BaslerVideoService>();
                    services.AddSingleton<IpVideoService>();
                    services.AddSingleton<FileVideoService>();

                    services.AddSingleton<QualityModelService>();
                    services.AddSingleton<TrainingService>();
                    services.AddSingleton<ModelSettings>(provider =>
                        provider.GetRequiredService<AppConfig>().ModelSettings);

                    // ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<TrainingViewModel>();

                    // Views
                    services.AddTransient<MainWindow>();
                    services.AddTransient<TrainingDialog>();

                    // Logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddDebug();
                    });
                })
                .Build();

            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private AppConfig LoadConfiguration()
        {
            try
            {
                var configPath = "appsettings.json";
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load configuration: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return new AppConfig();
        }

        private async void Application_Exit(object sender, ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
    }
}