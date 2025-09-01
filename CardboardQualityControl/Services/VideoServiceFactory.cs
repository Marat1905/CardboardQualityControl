using CardboardQualityControl.Models;
using CardboardQualityControl.Services;
using Microsoft.Extensions.DependencyInjection;

public class VideoServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppConfig _config;

    public VideoServiceFactory(IServiceProvider serviceProvider, AppConfig config)
    {
        _serviceProvider = serviceProvider;
        _config = config;
    }

    public IVideoService CreateVideoService(VideoSourceType sourceType)
    {
        return sourceType switch
        {
            VideoSourceType.Basler => _serviceProvider.GetRequiredService<BaslerVideoService>(),
            VideoSourceType.IpCamera => _serviceProvider.GetRequiredService<IpVideoService>(),
            VideoSourceType.FileVideo => _serviceProvider.GetRequiredService<FileVideoService>(),
            _ => throw new NotSupportedException($"Video source '{sourceType}' is not supported")
        };
    }

    // Добавьте метод без параметров для обратной совместимости
    public IVideoService CreateVideoService()
    {
        return CreateVideoService(_config.CurrentVideoSourceType);
    }
}