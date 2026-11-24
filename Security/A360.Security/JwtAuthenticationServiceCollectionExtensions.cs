using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace A360.Security;

public static class JwtAuthenticationServiceCollectionExtensions
{
    /// <summary>Framework log categories that write request URLs including the query string.</summary>
    private static readonly string[] UrlLoggingCategories =
    [
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "Microsoft.AspNetCore.HttpLogging",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.WebSockets",
        "Yarp.ReverseProxy",
        "System.Net.Http.HttpClient",
    ];

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

        // WebSocket URLs carry the sign-in token (?access_token=). These categories log full request URLs, query
        // included, at Information/Debug: keep them at Warning whatever the configured levels say, so a lowered log
        // level for debugging can't write tokens to the logs. (Our own log lines only use Request.Path.)
        services.AddLogging(logging =>
        {
            foreach (var category in UrlLoggingCategories)
            {
                logging.AddFilter(category, LogLevel.Warning);
            }
        });

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
