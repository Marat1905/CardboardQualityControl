using CardboardQualityControl.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CardboardQualityControl.Services
{
    public class VideoServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AppConfig _config;

        public VideoServiceFactory(IServiceProvider serviceProvider, AppConfig config)
        {
            _serviceProvider = serviceProvider;
            _config = config;
        }

        public IVideoService CreateVideoService()
        {
            return _config.VideoSource.ToLower() switch
            {
                "basler" => _serviceProvider.GetRequiredService<BaslerVideoService>(),
                "ip" => _serviceProvider.GetRequiredService<IpVideoService>(),
                "file" => _serviceProvider.GetRequiredService<FileVideoService>(),
                _ => throw new NotSupportedException($"Video source '{_config.VideoSource}' is not supported")
            };
        }
    }
}