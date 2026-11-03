using Microsoft.AspNetCore.Http;

namespace A360.Security;

public sealed record CurrentUser(
    string? Id,
    string? UserId,
    string UserName,
    string? ClientId,
    string? TenantId);

public interface ICurrentUserAccessor
{
    /// <summary>The user signed in with the current request's bearer token, or null when there is none.</summary>
    CurrentUser? GetCurrentUser();
}

public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUser? GetCurrentUser()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userName = principal.FindFirst(A360ClaimTypes.UserName)?.Value;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        return new CurrentUser(
            principal.FindFirst(A360ClaimTypes.Subject)?.Value,
            principal.FindFirst(A360ClaimTypes.UserId)?.Value,
            userName,
            principal.FindFirst(A360ClaimTypes.ClientId)?.Value,
            principal.FindFirst(A360ClaimTypes.TenantId)?.Value);
    }
}
