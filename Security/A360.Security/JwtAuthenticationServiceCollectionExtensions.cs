using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace A360.Security;

public static class JwtAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddA360JwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = JwtSettings.FromConfiguration(configuration);

        services.AddSingleton(jwtSettings);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwtSettings.CreateSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = A360ClaimTypes.UserName,
                    RoleClaimType = A360ClaimTypes.Role
                };
            });

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        return services;
    }
}
