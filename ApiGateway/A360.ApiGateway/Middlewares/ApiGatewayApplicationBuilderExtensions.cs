using A360.ApiGateway.Swagger;
using Microsoft.Extensions.Caching.Memory;

namespace A360.ApiGateway.Middlewares;

public static class ApiGatewayApplicationBuilderExtensions
{
    private const string MergedSwaggerCacheKey = "MergedSwaggerDocument";

    public static WebApplication UseApiGatewayMiddlewares(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseMiddleware<GatewayAuthenticationMiddleware>();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();

            app.MapGet("/openapi/all.json", async (
                IConfiguration configuration,
                IHttpClientFactory httpClientFactory,
                IMemoryCache cache,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                if (cache.TryGetValue(MergedSwaggerCacheKey, out string? cached) && cached is not null)
                {
                    return Results.Content(cached, "application/json");
                }

                var sources = SwaggerAggregator.GetSources(configuration);
                var merged = await SwaggerAggregator.BuildMergedDocumentAsync(
                    sources,
                    httpClientFactory,
                    loggerFactory.CreateLogger("SwaggerAggregator"),
                    cancellationToken);

                var json = merged.ToJsonString();
                cache.Set(MergedSwaggerCacheKey, json, TimeSpan.FromSeconds(30));

                return Results.Content(json, "application/json");
            })
                .WithName("MergedSwagger")
                .WithTags("Gateway")
                .ExcludeFromDescription();

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/all.json", "All Services (Aggregated)");
                options.SwaggerEndpoint("/project/swagger/v1/swagger.json", "Project Service");
                options.SwaggerEndpoint("/user-account/swagger/v1/swagger.json", "User Account Service");
                options.SwaggerEndpoint("/device/swagger/v1/swagger.json", "Device Service");
                options.SwaggerEndpoint("/media/swagger/v1/swagger.json", "Media Service");
                options.SwaggerEndpoint("/asset/swagger/v1/swagger.json", "Asset Service");
                options.SwaggerEndpoint("/eventlog/swagger/v1/swagger.json", "Event Log Service");
                options.SwaggerEndpoint("/maintenance/swagger/v1/swagger.json", "Maintenance Service");
                options.SwaggerEndpoint("/wip/swagger/v1/swagger.json", "WIP Service");
                options.SwaggerEndpoint("/locating/swagger/v1/swagger.json", "Locating Service");
                options.SwaggerEndpoint("/dashboard/swagger/v1/swagger.json", "Dashboard Service");
                options.SwaggerEndpoint("/report/swagger/v1/swagger.json", "Report Service");
                options.SwaggerEndpoint("/tracking/swagger/v1/swagger.json", "Tracking & IoT Service");

                options.RoutePrefix = "swagger";
            });
        }

        app.MapGet("/gateway/health", () => Results.Ok(new { status = "Healthy", service = "ApiGateway" }))
            .WithName("GatewayHealth")
            .WithTags("Gateway");

        app.MapGet("/gateway/swagger/downstream", () => Results.Ok(new[]
            {
                new
                {
                    service = "Project",
                    gatewaySwaggerUrl = "/project/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5254",
                    healthUrl = "/project/health"
                },
                new
                {
                    service = "UserAccount",
                    gatewaySwaggerUrl = "/user-account/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5018",
                    healthUrl = "/user-account/health"
                },
                new
               {
                    service = "Device",
                    gatewaySwaggerUrl = "/device/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5077",
                    healthUrl = "/device/health"
               },
                new
               {
                    service = "Media",
                    gatewaySwaggerUrl = "/media/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5300",
                    healthUrl = "/media/health"
               },
                new
               {
                    service = "EventLog",
                    gatewaySwaggerUrl = "/eventlog/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5420",
                    healthUrl = "/eventlog/health"
               },
                new
               {
                    service = "Maintenance",
                    gatewaySwaggerUrl = "/maintenance/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5430",
                    healthUrl = "/maintenance/health"
               },
                new
               {
                    service = "Wip",
                    gatewaySwaggerUrl = "/wip/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5440",
                    healthUrl = "/wip/health"
               },
                new
               {
                    service = "Locating",
                    gatewaySwaggerUrl = "/locating/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5460",
                    healthUrl = "/locating/health"
               },
                new
               {
                    service = "Dashboard",
                    gatewaySwaggerUrl = "/dashboard/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5470",
                    healthUrl = "/dashboard/health"
               },
                new
               {
                    service = "Report",
                    gatewaySwaggerUrl = "/report/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5480",
                    healthUrl = "/report/health"
               },
                new
               {
                    service = "Tracking",
                    gatewaySwaggerUrl = "/tracking/swagger/v1/swagger.json",
                    serviceBaseUrl = "http://172.16.100.26:5490",
                    healthUrl = "/tracking/health"
               }
            }))
            .WithName("GatewayDownstreamSwagger")
            .WithTags("Gateway");

        app.MapReverseProxy();

        return app;
    }
}
