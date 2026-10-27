using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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

                // Browsers cannot set headers on a WebSocket, so those requests may pass the token as ?access_token=.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"].ToString();
                        if (IsWebSocketRequest(context.HttpContext) && !string.IsNullOrEmpty(accessToken))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        return services;
    }

    // Read from the request itself: HttpContext.WebSockets only works where UseWebSockets is registered,
    // and the gateway proxies WebSockets without it.
    private static bool IsWebSocketRequest(HttpContext context)
    {
        var extendedConnect = context.Features.Get<IHttpExtendedConnectFeature>();
        if (extendedConnect?.IsExtendedConnect == true)
        {
            return string.Equals(extendedConnect.Protocol, "websocket", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(context.Request.Headers.Upgrade.ToString(), "websocket", StringComparison.OrdinalIgnoreCase);
    }
}
