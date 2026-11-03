using A360.Security;
using Yarp.ReverseProxy;

namespace A360.ApiGateway.IoC;

public static class ApiGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddApiGatewayServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"));

        services.AddA360JwtAuthentication(configuration);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddHttpClient("SwaggerAggregator");
        services.AddMemoryCache();

        return services;
    }
}
