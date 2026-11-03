using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace A360.Media.Client;

public static class MediaClientServiceCollectionExtensions
{
    public const string SectionName = "MediaService";

    public static IServiceCollection AddMediaStorageClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration[$"{SectionName}:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("MediaService:BaseUrl configuration is required.");
        }

        services.AddHttpClient<IMediaStorageClient, MediaStorageClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
