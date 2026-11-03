using A360.UserAccount.Api.Contracts;
using A360.UserAccount.Repository.Repositories;

namespace A360.UserAccount.Api.Validation;

internal static class UserAccountRelationshipValidator
{
    public static async Task<IDictionary<string, string[]>> ValidateRelationshipsAsync(
        this CreateUserRequest request,
        IRoleRepository roleRepository,
        CancellationToken cancellationToken)
    {
        return await ValidateRoleExistsAsync(request.UserRoleId, roleRepository, cancellationToken);
    }

    public static async Task<IDictionary<string, string[]>> ValidateRelationshipsAsync(
        this UpdateUserRequest request,
        IRoleRepository roleRepository,
        CancellationToken cancellationToken)
    {
        return await ValidateRoleExistsAsync(request.UserRoleId, roleRepository, cancellationToken);
    }

    private static async Task<IDictionary<string, string[]>> ValidateRoleExistsAsync(
        string? roleIdentifier,
        IRoleRepository roleRepository,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrorBuilder();

        if (string.IsNullOrWhiteSpace(roleIdentifier))
        {
            return errors.ToDictionary();
        }

        var roleExists = await roleRepository.RoleExistsAsync(roleIdentifier, cancellationToken);
        if (!roleExists)
        {
            errors.Error(nameof(CreateUserRequest.UserRoleId), "Role was not found.");
        }

        return errors.ToDictionary();
    }
}
