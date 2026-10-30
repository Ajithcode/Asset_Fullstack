using A360.Security;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

internal static class UserMappings
{
    public static User ToEntity(this CreateUserRequest request, CurrentUser? currentUser)
    {
        return new User
        {
            // Generated on the server so every user gets a stable id independent of the Mongo _id.
            UserId = Guid.NewGuid().ToString(),
            Name = request.Name!.Trim(),
            Username = request.Username!.Trim().ToLowerInvariant(),
            Image = Optional(request.Image),
            Email = request.Email!.Trim().ToLowerInvariant(),
            EmployeeId = Optional(request.EmployeeId),
            Department = Optional(request.Department),
            JobTitle = Optional(request.JobTitle),
            Phone = Optional(request.Phone),
            Location = Optional(request.Location),
            TimeZone = Optional(request.TimeZone),
            PrimaryRole = request.PrimaryRole!.Trim(),
            SecondaryRoles = CleanList(request.SecondaryRoles),
            AccessScope = Optional(request.AccessScope),
            MfaEnabled = request.MfaEnabled ?? false,
            ModuleAccess = CleanList(request.ModuleAccess),
            Status = request.AccountStatus!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> CleanList(List<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
