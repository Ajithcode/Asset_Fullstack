using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class AccessPolicyEndpoints
{
    private const string EntityType = "AccessPolicy";

    public static RouteGroupBuilder MapAccessPolicyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/access-policies").WithTags("Access Policies");

        group.MapGet("", GetAccessPoliciesAsync).WithName("GetAccessPolicies");
        group.MapGet("/{policyId}", GetAccessPolicyByIdAsync).WithName("GetAccessPolicyById");
        group.MapPost("", CreateAccessPolicyAsync).WithName("CreateAccessPolicy");
        group.MapPut("/{policyId}", UpdateAccessPolicyAsync).WithName("UpdateAccessPolicy");
        group.MapDelete("/{policyId}", DeleteAccessPolicyAsync).WithName("DeleteAccessPolicy");

        return group;
    }

    private static async Task<IResult> GetAccessPoliciesAsync(
        string? tenantId,
        string? clientId,
        string? policyType,
        string? status,
        IAccessPolicyRepository repository,
        IRoleRepository roleRepository,
        CancellationToken cancellationToken)
    {
        var policies = await repository.GetAsync(tenantId, clientId, policyType, status, cancellationToken);
        var rolesById = await LoadRolesAsync(policies.SelectMany(policy => policy.AssignedRoleIds), roleRepository, cancellationToken);

        return Results.Ok(policies.Select(policy => AccessPolicyResponse.FromEntity(policy, rolesById)));
    }

    private static async Task<IResult> GetAccessPolicyByIdAsync(
        string policyId,
        IAccessPolicyRepository repository,
        IRoleRepository roleRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var policy = await repository.GetByPolicyIdAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return Results.NotFound();
        }

        var rolesById = await LoadRolesAsync(policy.AssignedRoleIds, roleRepository, cancellationToken);

        await eventLogger.LogAsync(EntityType, policy.Id, policy.PolicyName, EventAction.Viewed, cancellationToken);

        return Results.Ok(AccessPolicyResponse.FromEntity(policy, rolesById));
    }

    private static async Task<IResult> CreateAccessPolicyAsync(
        CreateAccessPolicyRequest request,
        IAccessPolicyRepository repository,
        IRoleRepository roleRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var policy = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.PolicyCodeExistsAsync(policy.PolicyCode, policy.TenantId, cancellationToken: cancellationToken))
        {
            return Results.Conflict(new { message = $"Policy code '{policy.PolicyCode}' already exists" });
        }

        var rolesById = await LoadRolesAsync(policy.AssignedRoleIds, roleRepository, cancellationToken);
        var missingRoleIds = policy.AssignedRoleIds.Where(roleId => !rolesById.ContainsKey(roleId)).ToList();
        if (missingRoleIds.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.AssignedRoles)] = [$"Roles not found: {string.Join(", ", missingRoleIds)}"]
            });
        }

        policy = await repository.CreateAsync(policy, cancellationToken);

        await eventLogger.LogAsync(EntityType, policy.Id, policy.PolicyName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/access-policies/{policy.PolicyId}", AccessPolicyResponse.FromEntity(policy, rolesById));
    }

    private static async Task<IResult> UpdateAccessPolicyAsync(
        string policyId,
        UpdateAccessPolicyRequest request,
        IAccessPolicyRepository repository,
        IRoleRepository roleRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var policy = await repository.GetByPolicyIdAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return NotFound(policyId);
        }

        request.ApplyTo(policy, currentUserAccessor.GetCurrentUser());

        if (await repository.PolicyCodeExistsAsync(policy.PolicyCode, policy.TenantId, policy.PolicyId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Policy code '{policy.PolicyCode}' already exists" });
        }

        var rolesById = await LoadRolesAsync(policy.AssignedRoleIds, roleRepository, cancellationToken);
        var missingRoleIds = policy.AssignedRoleIds.Where(roleId => !rolesById.ContainsKey(roleId)).ToList();
        if (missingRoleIds.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.AssignedRoles)] = [$"Roles not found: {string.Join(", ", missingRoleIds)}"]
            });
        }

        if (!await repository.UpdateAsync(policy.Id, policy, cancellationToken))
        {
            return NotFound(policyId);
        }

        await eventLogger.LogAsync(EntityType, policy.Id, policy.PolicyName, EventAction.Updated, cancellationToken);

        return Results.Ok(AccessPolicyResponse.FromEntity(policy, rolesById));
    }

    // Soft delete: the policy stays in the collection with IsDeleted set.
    private static async Task<IResult> DeleteAccessPolicyAsync(
        string policyId,
        IAccessPolicyRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var policy = await repository.GetByPolicyIdAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return NotFound(policyId);
        }

        policy.IsDeleted = true;
        policy.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        policy.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(policy.Id, policy, cancellationToken))
        {
            return NotFound(policyId);
        }

        await eventLogger.LogAsync(EntityType, policy.Id, policy.PolicyName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static IResult NotFound(string policyId)
    {
        return Results.NotFound(new { message = $"Access policy '{policyId}' was not found." });
    }

    private static async Task<IReadOnlyDictionary<string, Role>> LoadRolesAsync(
        IEnumerable<string> roleIds,
        IRoleRepository roleRepository,
        CancellationToken cancellationToken)
    {
        var roles = await roleRepository.GetByRoleIdsAsync(roleIds.Distinct().ToList(), cancellationToken);

        return roles.ToDictionary(role => role.RoleId);
    }
}
