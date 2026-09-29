using Microsoft.Extensions.FileProviders;
using A360.Media.Api.Endpoints;

namespace A360.Media.Api.Middlewares;

public static class MediaApplicationBuilderExtensions
{
    public static WebApplication UseMediaApiMiddlewares(
        this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(uploadsFolder);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsFolder),
            RequestPath = "/uploads"
        });

        if (app.Environment.IsDevelopment() ||
            app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () =>
            Results.Ok(new
            {
                status = "Healthy",
                service = "Media"
            }))
            .WithName("MediaHealth")
            .WithTags("Health");

        app.MapMediaEndpoints();

        return app;
    }
}
