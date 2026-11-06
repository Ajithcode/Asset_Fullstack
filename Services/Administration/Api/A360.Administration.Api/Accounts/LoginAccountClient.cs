using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Accounts;

/// <summary>Outcome of a call to the UserAccount service: the account id on success, or a readable error.</summary>
public sealed record LoginAccountResult(bool Succeeded, string? AccountId, string? Error, bool NotFound = false)
{
    public static LoginAccountResult Ok(string? accountId = null) => new(true, accountId, null);

    public static LoginAccountResult Fail(string error, bool notFound = false) => new(false, null, error, notFound);
}

/// <summary>
/// Keeps the sign-in account in the UserAccount service in step with a User Management user. Login reads that
/// service's Users collection, so the password is sent there (it hashes it); Administration stores only the account id.
/// </summary>
public sealed class LoginAccountClient(HttpClient http, ILogger<LoginAccountClient> logger)
{
    /// <summary>UserAccount role used when no role there has the user's primary role name.</summary>
    private const string DefaultRoleId = "R-USER";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<LoginAccountResult> CreateAsync(User user, string password, CancellationToken cancellationToken)
    {
        try
        {
            var body = new
            {
                userName = user.Username,
                shortName = ShortName(user),
                contactNo = ContactNo(user),
                email = user.Email,
                loginPassword = password,
                activeDirectoryUserName = string.Empty,
                userRoleId = await ResolveRoleIdAsync(user.PrimaryRole, cancellationToken),
                createdBy = string.IsNullOrWhiteSpace(user.CreatedBy) ? "User Management" : user.CreatedBy,
                clientId = user.ClientId,
                tenantId = user.TenantId
            };
            using var response = await http.PostAsJsonAsync("api/users", body, Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return LoginAccountResult.Fail(await ErrorAsync(response, "The sign-in account could not be created.", cancellationToken));
            }

            var created = await response.Content.ReadFromJsonAsync<AccountDto>(Json, cancellationToken);
            if (string.IsNullOrWhiteSpace(created?.Id))
            {
                return LoginAccountResult.Fail("The sign-in service returned no account id.");
            }

            // New accounts start active; a suspended user is switched off straight away
            if (LoginStatus(user) == "Inactive")
            {
                var update = await UpdateAsync(user, created.Id, null, cancellationToken);
                if (!update.Succeeded)
                {
                    await DeleteAsync(created.Id, cancellationToken);
                    return update;
                }
            }

            return LoginAccountResult.Ok(created.Id);
        }
        catch (HttpRequestException exception)
        {
            return Unreachable(exception);
        }
    }

    /// <param name="password">New password, or null / blank to keep the current one.</param>
    public async Task<LoginAccountResult> UpdateAsync(User user, string accountId, string? password, CancellationToken cancellationToken)
    {
        try
        {
            var body = new
            {
                userName = user.Username,
                shortName = ShortName(user),
                contactNo = ContactNo(user),
                email = user.Email,
                loginPassword = string.IsNullOrWhiteSpace(password) ? null : password,
                activeDirectoryUserName = string.Empty,
                userRoleId = await ResolveRoleIdAsync(user.PrimaryRole, cancellationToken),
                updatedBy = string.IsNullOrWhiteSpace(user.UpdatedBy) ? "User Management" : user.UpdatedBy,
                status = LoginStatus(user)
            };
            using var response = await http.PutAsJsonAsync($"api/users/{Uri.EscapeDataString(accountId)}", body, Json, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return LoginAccountResult.Fail("The sign-in account no longer exists.", notFound: true);
            }

            return response.IsSuccessStatusCode
                ? LoginAccountResult.Ok(accountId)
                : LoginAccountResult.Fail(await ErrorAsync(response, "The sign-in account could not be updated.", cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return Unreachable(exception);
        }
    }

    /// <summary>Deletes the sign-in account; one that is already gone counts as deleted.</summary>
    public async Task<LoginAccountResult> DeleteAsync(string accountId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.DeleteAsync($"api/users/{Uri.EscapeDataString(accountId)}", cancellationToken);
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound
                ? LoginAccountResult.Ok()
                : LoginAccountResult.Fail(await ErrorAsync(response, "The sign-in account could not be deleted.", cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return Unreachable(exception);
        }
    }

    /// <summary>The UserAccount role with the same name as the primary role, else the default user role.</summary>
    private async Task<string> ResolveRoleIdAsync(string primaryRole, CancellationToken cancellationToken)
    {
        try
        {
            var roles = await http.GetFromJsonAsync<List<RoleDto>>("api/roles", Json, cancellationToken) ?? [];
            var match = roles.FirstOrDefault(role => string.Equals(role.RoleName?.Trim(), primaryRole.Trim(), StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(match?.RoleId) ? DefaultRoleId : match.RoleId;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            logger.LogWarning(exception, "Could not read UserAccount roles; using {RoleId}.", DefaultRoleId);
            return DefaultRoleId;
        }
    }

    // Suspended users can't sign in; Active and Pending users can (an admin-set password needs no invitation)
    private static string LoginStatus(User user) =>
        string.Equals(user.Status, "Suspended", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";

    private static string ShortName(User user)
    {
        var first = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? user.Username : first;
    }

    // The UserAccount service requires a contact number
    private static string ContactNo(User user) => string.IsNullOrWhiteSpace(user.Phone) ? "N/A" : user.Phone;

    private LoginAccountResult Unreachable(HttpRequestException exception)
    {
        logger.LogError(exception, "The UserAccount service could not be reached.");
        return LoginAccountResult.Fail("The sign-in service could not be reached. Try again in a moment.");
    }

    /// <summary>The message from a { message } or validation { errors } body, else the fallback.</summary>
    private static async Task<string> ErrorAsync(HttpResponseMessage response, string fallback, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorDto>(Json, cancellationToken);
            var first = body?.Errors?.Values.SelectMany(messages => messages).FirstOrDefault();
            return first ?? body?.Message ?? fallback;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return fallback;
        }
    }

    private sealed record AccountDto(string? Id);

    private sealed record RoleDto(string? RoleId, string? RoleName);

    private sealed record ErrorDto(string? Message, Dictionary<string, string[]>? Errors);
}
