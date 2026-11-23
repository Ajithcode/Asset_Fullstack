using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using A360.Security;
using Microsoft.IdentityModel.Tokens;

namespace A360.Administration.Api.Accounts;

/// <summary>
/// Sends the signed-in caller's bearer token on calls to other services (e.g. the UserAccount service), which require
/// a signed-in user for every /api request. An invited user accepting an invitation has no token yet, so for the public
/// /api/invitations calls a short-lived service token (same signing key, valid for two minutes) is sent instead; the
/// invitation endpoint issues it only after the invitation token itself has been checked.
/// </summary>
public sealed class ForwardAuthorizationHandler(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : DelegatingHandler
{
    private const string AdministratorRoleId = "R-ADMINISTRATOR";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var http = httpContextAccessor.HttpContext;
        var authorization = http?.Request.Headers.Authorization.ToString();
        if (request.Headers.Authorization is null)
        {
            if (!string.IsNullOrWhiteSpace(authorization))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            }
            else if (http?.Request.Path.StartsWithSegments("/api/invitations", StringComparison.OrdinalIgnoreCase) == true)
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {CreateServiceToken()}");
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private string CreateServiceToken()
    {
        var settings = JwtSettings.FromConfiguration(configuration);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(A360ClaimTypes.Subject, "invitation-service"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(A360ClaimTypes.UserId, "invitation-service"),
                new Claim(A360ClaimTypes.UserName, "invitation-service"),
                new Claim(A360ClaimTypes.Email, "invitation-service@asset360.local"),
                new Claim(A360ClaimTypes.RoleId, AdministratorRoleId),
                new Claim(A360ClaimTypes.Role, "Administrator")
            ],
            notBefore: now,
            expires: now.AddMinutes(2),
            signingCredentials: new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
