using A360.UserAccount.Api.Endpoints;

namespace A360.UserAccount.Api.Middlewares;

public static class UserAccountApplicationBuilderExtensions
{
    public static WebApplication UseUserAccountApiMiddlewares(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "UserAccount" }))
            .WithName("UserAccountHealth")
            .WithTags("Health");

        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        app.MapRoleEndpoints();

        return app;
    }
}
