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

        // Role-based access for writes reads the caller's permissions from the Administration service (same host)
        services.AddHttpClient(Middlewares.ModulePermissionMiddleware.AdministrationClientName, client =>
        {
            client.BaseAddress = new Uri(configuration["Services:AdministrationUrl"] ?? "http://localhost:5460/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
