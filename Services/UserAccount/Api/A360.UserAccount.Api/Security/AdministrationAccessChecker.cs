using System.Net.Http.Headers;
using System.Text.Json;
using A360.Security;

namespace A360.UserAccount.Api.Security;

/// <summary>
/// Checks the caller's permission-matrix rights, the same way the API gateway and the access guard do: GET /api/users/me
/// on the Administration service with the caller's own token. Used where this service needs a module permission (an
/// administrator resetting another user's password needs Edit on Administration).
/// </summary>
public sealed class AdministrationAccessChecker(IHttpClientFactory httpClientFactory, ILogger<AdministrationAccessChecker> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>True / false when the rights were read; null when they could not be checked.</summary>
    public async Task<bool?> AllowsAsync(string authorization, string module, string action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return false;
        }

        try
        {
            var client = httpClientFactory.CreateClient(A360AccessGuardExtensions.AdministrationClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/me");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GET /api/users/me returned {Status}; permission check failed.", (int)response.StatusCode);
                return null;
            }

            var access = await response.Content.ReadFromJsonAsync<CallerAccess>(Json, cancellationToken);
            if (access is null)
            {
                return null;
            }

            return access.FullAccess
                || (access.Modules ?? []).Any(m => string.Equals(m.Module, module, StringComparison.OrdinalIgnoreCase)
                    && (m.Actions ?? []).Contains(action, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or FormatException)
        {
            logger.LogWarning(exception, "Could not read the caller's access.");
            return null;
        }
    }

    private sealed record ModuleAccess(string Module, List<string>? Actions);

    private sealed record CallerAccess(bool FullAccess, List<ModuleAccess>? Modules);
}
