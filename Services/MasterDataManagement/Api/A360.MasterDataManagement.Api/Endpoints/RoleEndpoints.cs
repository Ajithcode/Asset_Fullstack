using System.Globalization;
using System.Text;
using RoleEntity = A360.MasterDataManagement.Domain.Entities.Role;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Domain.ValueObjects;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/roles")
            .WithTags("Roles");

        group.MapGet("", GetRolesAsync)
            .WithName("GetRoles");

        // Stat cards: total / active / custom roles and users assigned.
        group.MapGet("/summary", GetRoleSummaryAsync)
            .WithName("GetRoleSummary");

        // Dropdown values for role type, data scope and the matrix columns.
        group.MapGet("/options", GetRoleOptions)
            .WithName("GetRoleOptions");

        // Empty permission matrix (every active module, nothing granted) for the "Create Role" screen.
        group.MapGet("/permission-matrix", GetPermissionMatrixTemplateAsync)
            .WithName("GetRolePermissionMatrixTemplate");

        group.MapGet("/export", ExportRolesAsync)
            .WithName("ExportRoles");

        group.MapPost("/import", ImportRolesAsync)
            .WithName("ImportRoles");

        group.MapGet("/{id}", GetRoleByIdAsync)
            .WithName("GetRoleById");

        group.MapGet("/{id}/permission-matrix", GetRolePermissionMatrixAsync)
            .WithName("GetRolePermissionMatrix");

        group.MapPut("/{id}/permission-matrix", UpdateRolePermissionMatrixAsync)
            .WithName("UpdateRolePermissionMatrix");

        group.MapGet("/{id}/users", GetRoleUsersAsync)
            .WithName("GetRoleUsers");

        group.MapPost("", CreateRoleAsync)
            .WithName("CreateRole");

        group.MapPost("/{id}/duplicate", DuplicateRoleAsync)
            .WithName("DuplicateRole");

        group.MapPut("/{id}", UpdateRoleAsync)
            .WithName("UpdateRole");

        group.MapPatch("/{id}/status", UpdateRoleStatusAsync)
            .WithName("UpdateRoleStatus");

        group.MapDelete("/{id}", DeleteRoleAsync)
            .WithName("DeleteRole");

        return group;
    }

    private static async Task<IResult> GetRolesAsync(
        string? search,
        string? status,
        string? type,
        string? clientId,
        IRoleRepository repository,
        ISystemUserRepository users,
        CancellationToken cancellationToken)
    {
        var roles = await repository.SearchAsync(
            new RoleFilter(search, status, type, clientId),
            cancellationToken);

        var userCounts = await users.CountByPrimaryRoleAsync(
            roles.Select(x => x.Id),
            cancellationToken);

        return Results.Ok(
            roles.Select(x => RoleResponse.FromEntity(
                x,
                userCounts.GetValueOrDefault(x.Id))));
    }

    private static async Task<IResult> GetRoleSummaryAsync(
        string? clientId,
        IRoleRepository repository,
        ISystemUserRepository users,
        CancellationToken cancellationToken)
    {
        var roles = await repository.SearchAsync(
            new RoleFilter(null, null, null, clientId),
            cancellationToken);

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var total = roles.Count;
        var createdThisMonth = roles.Count(x => x.CreatedAt >= monthStart);
        var active = roles.Count(x => string.Equals(x.Status, MasterDataStatuses.Active, StringComparison.OrdinalIgnoreCase));
        var system = roles.Count(x => RoleContractDefaults.IsSystem(x.Type));
        var custom = total - system;

        var roleIds = roles.Select(x => x.Id).ToHashSet();

        var allUsers = await users.SearchAsync(
            new SystemUserFilter(null, null, null, null, clientId),
            cancellationToken);

        var assigned = allUsers
            .Where(x => x.PrimaryRole?.Id is not null && roleIds.Contains(x.PrimaryRole.Id))
            .ToList();

        var assignedNow = assigned.Count;
        var assignedLastMonth = assigned.Count(x => x.CreatedAt < monthStart);

        var change = assignedLastMonth == 0
            ? (assignedNow > 0 ? 100 : 0)
            : Math.Round((assignedNow - assignedLastMonth) * 100.0 / assignedLastMonth, 1);

        return Results.Ok(new RoleSummaryResponse(
            total,
            createdThisMonth,
            active,
            Percent(active, total),
            custom,
            Percent(custom, total),
            system,
            assignedNow,
            assignedLastMonth,
            change));
    }

    private static IResult GetRoleOptions()
    {
        return Results.Ok(new RoleOptionsResponse(
            [RoleTypes.CustomRole, RoleTypes.SystemRole],
            RoleDataScopes.All,
            ModuleActions.All,
            [RoleContractDefaults.FullAccess, RoleContractDefaults.PartialAccess, RoleContractDefaults.NoAccess]));
    }

    private static async Task<IResult> GetPermissionMatrixTemplateAsync(
        string? clientId,
        IModuleAccessRepository modules,
        CancellationToken cancellationToken)
    {
        var activeModules = await modules.SearchAsync(
            new ModuleAccessFilter(null, MasterDataStatuses.Active, null, clientId),
            cancellationToken);

        return Results.Ok(new RolePermissionMatrixResponse(
            null,
            null,
            activeModules.Select(x => RoleContractDefaults.ToMatrixRow(x, null)).ToList()));
    }

    private static async Task<IResult> GetRoleByIdAsync(
        string id,
        IRoleRepository repository,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> GetRolePermissionMatrixAsync(
        string id,
        IRoleRepository repository,
        IModuleAccessRepository modules,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(await BuildMatrixAsync(role, modules, cancellationToken));
    }

    private static async Task<IResult> UpdateRolePermissionMatrixAsync(
        string id,
        UpdateRolePermissionMatrixRequest request,
        IRoleRepository repository,
        IModuleAccessRepository modules,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        role.ModulePermissions = request.ModulePermissions!.Select(x => x.ToValue()).ToList();
        role.UpdatedBy = request.UpdatedBy;
        role.UpdatedAt = DateTime.UtcNow;

        var moduleError = await ResolveModulePermissionsAsync(
            role,
            modules,
            cancellationToken);

        if (moduleError is not null)
        {
            return moduleError;
        }

        var updated = await repository.UpdateAsync(
            id,
            role,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await BuildMatrixAsync(role, modules, cancellationToken));
    }

    private static async Task<IResult> GetRoleUsersAsync(
        string id,
        string? search,
        string? status,
        ISystemUserRepository users,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var roleUsers = await users.SearchAsync(
            new SystemUserFilter(search, status, id, null, null),
            cancellationToken);

        return Results.Ok(roleUsers.Select(x => new RoleUserResponse(
            x.Id,
            x.FullName,
            x.Email,
            x.Department is null ? null : MasterDataReferenceDto.FromReference(x.Department),
            x.Status)));
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        IRoleRepository repository,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ILocationRepository locations,
        IDepartmentRepository departments,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var (role, error) = await BuildNewRoleAsync(
            request,
            repository,
            permissions,
            modules,
            locations,
            departments,
            cancellationToken);

        if (error is not null)
        {
            return error;
        }

        role = await repository.CreateAsync(
            role!,
            cancellationToken);

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/roles/{role.Id}",
            await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> DuplicateRoleAsync(
        string id,
        DuplicateRoleRequest? request,
        IRoleRepository repository,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var source = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (source is null)
        {
            return Results.NotFound();
        }

        string code;

        if (!string.IsNullOrWhiteSpace(request?.Code))
        {
            code = request.Code.Trim();

            if (await repository.GetByCodeAsync(code, source.ClientId, cancellationToken) is not null)
            {
                return Results.Conflict(
                    new { message = $"A role with code '{code}' already exists." });
            }
        }
        else
        {
            code = await NextFreeCodeAsync($"{source.Code}_COPY", source.ClientId, repository, cancellationToken);
        }

        var copy = new RoleEntity
        {
            RoleName = string.IsNullOrWhiteSpace(request?.RoleName) ? $"{source.RoleName} (Copy)" : request.RoleName.Trim(),
            Code = code,
            // A copy is always editable, even when it was made from a system role.
            Type = RoleTypes.CustomRole,
            Description = source.Description,
            Permissions = source.Permissions.Select(CopyReference).ToList(),
            ModuleAccess = source.ModuleAccess.Select(CopyReference).ToList(),
            ModulePermissions = source.ModulePermissions.Select(CopyPermission).ToList(),
            DataScope = source.DataScope,
            Locations = source.Locations.Select(CopyReference).ToList(),
            Departments = source.Departments.Select(CopyReference).ToList(),
            ApprovalLimit = source.ApprovalLimit,
            ApprovalCurrency = source.ApprovalCurrency,
            MfaRequired = source.MfaRequired,

            CreatedBy = request?.CreatedBy,
            ClientId = source.ClientId,
            TenantId = source.TenantId,
            Status = source.Status ?? MasterDataStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        copy = await repository.CreateAsync(
            copy,
            cancellationToken);

        await eventLogger.LogAsync(
            "Role",
            copy.Id,
            copy.RoleName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/roles/{copy.Id}",
            await ToResponseAsync(copy, users, cancellationToken));
    }

    private static async Task<IResult> UpdateRoleAsync(
        string id,
        UpdateRoleRequest request,
        IRoleRepository repository,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ILocationRepository locations,
        IDepartmentRepository departments,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        var previousName = role.RoleName;

        var error = await ApplyUpdateAsync(
            role,
            request,
            repository,
            permissions,
            modules,
            locations,
            departments,
            cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var updated = await repository.UpdateAsync(
            id,
            role,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != role.RoleName)
        {
            await users.UpdatePrimaryRoleNameAsync(
                id,
                role.RoleName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> UpdateRoleStatusAsync(
        string id,
        UpdateRoleStatusRequest request,
        IRoleRepository repository,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        role.Status = request.Status;
        role.UpdatedBy = request.UpdatedBy;
        role.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            role,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(role, users, cancellationToken));
    }

    private static async Task<IResult> DeleteRoleAsync(
        string id,
        IRoleRepository repository,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid role id." });
        }

        var role = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (role is null)
        {
            return Results.NotFound();
        }

        if (RoleContractDefaults.IsSystem(role.Type))
        {
            return Results.Conflict(
                new { message = "System roles cannot be deleted. Deactivate it instead." });
        }

        if (await users.AnyWithPrimaryRoleAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Users still have this as their primary role. Change their role first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Role",
            role.Id,
            role.RoleName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>Exports roles as CSV (default) or as JSON in the same shape that POST /import accepts.</summary>
    private static async Task<IResult> ExportRolesAsync(
        string? search,
        string? status,
        string? type,
        string? clientId,
        string? format,
        IRoleRepository repository,
        ISystemUserRepository users,
        CancellationToken cancellationToken)
    {
        var roles = await repository.SearchAsync(
            new RoleFilter(search, status, type, clientId),
            cancellationToken);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(roles.Select(ToImportShape));
        }

        var userCounts = await users.CountByPrimaryRoleAsync(
            roles.Select(x => x.Id),
            cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Role Name,Role Code,Role Type,Status,Description,Data Scope,Locations,Departments,Approval Limit,Approval Currency,MFA Required,Assigned Users,Module Permissions");

        foreach (var role in roles)
        {
            var matrix = string.Join("; ", role.ModulePermissions
                .Where(x => x.HasAny())
                .Select(x => $"{x.ModuleName}[{string.Join("|", GrantedActions(x))}]"));

            csv.AppendLine(string.Join(",",
                Csv(role.RoleName),
                Csv(role.Code),
                Csv(role.Type),
                Csv(role.Status),
                Csv(role.Description),
                Csv(role.DataScope),
                Csv(string.Join("; ", role.Locations.Select(x => x.Name))),
                Csv(string.Join("; ", role.Departments.Select(x => x.Name))),
                role.ApprovalLimit.ToString(CultureInfo.InvariantCulture),
                Csv(role.ApprovalCurrency),
                role.MfaRequired ? "Yes" : "No",
                userCounts.GetValueOrDefault(role.Id).ToString(CultureInfo.InvariantCulture),
                Csv(matrix)));
        }

        return Results.File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv",
            $"roles-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    /// <summary>
    /// Creates roles from a list. A row whose code already exists is updated when UpdateExisting is true,
    /// otherwise it is reported as failed. Each row succeeds or fails on its own.
    /// </summary>
    private static async Task<IResult> ImportRolesAsync(
        ImportRolesRequest request,
        IRoleRepository repository,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ILocationRepository locations,
        IDepartmentRepository departments,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (request.Roles is null || request.Roles.Count == 0)
        {
            return Results.BadRequest(
                new { message = "Roles must contain at least one role." });
        }

        var results = new List<ImportRoleResult>();

        for (var i = 0; i < request.Roles.Count; i++)
        {
            var row = request.Roles[i] with
            {
                CreatedBy = request.Roles[i].CreatedBy ?? request.CreatedBy,
                ClientId = request.Roles[i].ClientId ?? request.ClientId,
                TenantId = request.Roles[i].TenantId ?? request.TenantId
            };

            var existing = string.IsNullOrWhiteSpace(row.Code)
                ? null
                : await repository.GetByCodeAsync(row.Code.Trim(), row.ClientId, cancellationToken);

            if (existing is not null && request.UpdateExisting)
            {
                var update = new UpdateRoleRequest(
                    row.RoleName, row.Code, row.Type, row.Description,
                    row.Permissions, row.ModuleAccess, row.ModulePermissions,
                    row.DataScope, row.Locations, row.Departments,
                    row.ApprovalLimit, row.ApprovalCurrency, row.MfaRequired,
                    row.Status, row.CreatedBy);

                var updateError = await ApplyUpdateAsync(
                    existing, update, repository, permissions, modules, locations, departments, cancellationToken);

                if (updateError is not null)
                {
                    results.Add(new ImportRoleResult(i + 1, row.Code, "Failed", existing.Id, Describe(updateError)));
                    continue;
                }

                await repository.UpdateAsync(existing.Id, existing, cancellationToken);
                await eventLogger.LogAsync("Role", existing.Id, existing.RoleName, EventAction.Updated, cancellationToken);
                results.Add(new ImportRoleResult(i + 1, row.Code, "Updated", existing.Id, null));
                continue;
            }

            var (role, error) = await BuildNewRoleAsync(
                row, repository, permissions, modules, locations, departments, cancellationToken);

            if (error is not null)
            {
                results.Add(new ImportRoleResult(i + 1, row.Code, "Failed", null, Describe(error)));
                continue;
            }

            role = await repository.CreateAsync(role!, cancellationToken);
            await eventLogger.LogAsync("Role", role.Id, role.RoleName, EventAction.Created, cancellationToken);
            results.Add(new ImportRoleResult(i + 1, row.Code, "Created", role.Id, null));
        }

        return Results.Ok(new ImportRolesResponse(
            results.Count(x => x.Outcome == "Created"),
            results.Count(x => x.Outcome == "Updated"),
            results.Count(x => x.Outcome == "Failed"),
            results));
    }

    /// <summary>Validates a create request, applies CopyFromRoleId, checks the code is free and resolves references.</summary>
    private static async Task<(RoleEntity? Role, IResult? Error)> BuildNewRoleAsync(
        CreateRoleRequest request,
        IRoleRepository repository,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ILocationRepository locations,
        IDepartmentRepository departments,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return (null, Results.ValidationProblem(validationErrors));
        }

        var role = request.ToEntity();
        var matrixGiven = request.ModulePermissions is not null;

        if (!string.IsNullOrWhiteSpace(request.CopyFromRoleId))
        {
            var source = MongoObjectId.IsValid(request.CopyFromRoleId)
                ? await repository.GetByIdAsync(request.CopyFromRoleId, cancellationToken)
                : null;

            if (source is null)
            {
                return (null, Results.BadRequest(
                    new { message = $"Role '{request.CopyFromRoleId}' to copy from was not found." }));
            }

            if (request.Permissions is null)
            {
                role.Permissions = source.Permissions.Select(CopyReference).ToList();
            }

            if (request.ModulePermissions is null && source.ModulePermissions.Count > 0)
            {
                role.ModulePermissions = source.ModulePermissions.Select(CopyPermission).ToList();
                matrixGiven = true;
            }
            else if (request.ModuleAccess is null && !matrixGiven)
            {
                role.ModuleAccess = source.ModuleAccess.Select(CopyReference).ToList();
            }
        }

        var existing = await repository.GetByCodeAsync(
            role.Code,
            role.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return (null, Results.Conflict(
                new { message = $"A role with code '{role.Code}' already exists." }));
        }

        var referenceError = await ResolveReferencesAsync(
            role,
            matrixGiven,
            permissions,
            modules,
            locations,
            departments,
            cancellationToken);

        return referenceError is not null ? (null, referenceError) : (role, null);
    }

    private static async Task<IResult?> ApplyUpdateAsync(
        RoleEntity role,
        UpdateRoleRequest request,
        IRoleRepository repository,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ILocationRepository locations,
        IDepartmentRepository departments,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        request.ApplyTo(role);

        var existing = await repository.GetByCodeAsync(
            role.Code,
            role.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != role.Id)
        {
            return Results.Conflict(
                new { message = $"A role with code '{role.Code}' already exists." });
        }

        return await ResolveReferencesAsync(
            role,
            request.ModulePermissions is not null,
            permissions,
            modules,
            locations,
            departments,
            cancellationToken);
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// When the permission matrix was supplied it is resolved and ModuleAccess is rebuilt from it.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        RoleEntity role,
        bool resolveMatrix,
        IPermissionRepository permissions,
        IModuleAccessRepository modules,
        ILocationRepository locations,
        IDepartmentRepository departments,
        CancellationToken cancellationToken)
    {
        var permissionsError = await MasterDataReferenceResolver.ResolvePermissionListAsync(
            role.Permissions,
            permissions,
            cancellationToken);

        if (permissionsError is not null)
        {
            return permissionsError;
        }

        var moduleError = resolveMatrix
            ? await ResolveModulePermissionsAsync(role, modules, cancellationToken)
            : await MasterDataReferenceResolver.ResolveModuleAccessListAsync(role.ModuleAccess, modules, cancellationToken);

        if (moduleError is not null)
        {
            return moduleError;
        }

        foreach (var reference in role.Locations)
        {
            var location = MongoObjectId.IsValid(reference.Id!)
                ? await locations.GetByIdAsync(reference.Id!, cancellationToken)
                : null;

            if (location is null)
            {
                return Results.BadRequest(
                    new { message = $"Location '{reference.Id}' was not found." });
            }

            reference.Name = location.LocationName;
        }

        return await MasterDataReferenceResolver.ResolveDepartmentsAsync(
            role.Departments,
            departments,
            cancellationToken);
    }

    /// <summary>
    /// Checks each matrix row's module exists, copies its name, drops actions the module does not offer
    /// and rows with nothing granted, then rebuilds ModuleAccess from what is left.
    /// </summary>
    private static async Task<IResult?> ResolveModulePermissionsAsync(
        RoleEntity role,
        IModuleAccessRepository modules,
        CancellationToken cancellationToken)
    {
        var resolved = new List<RoleModulePermission>();

        foreach (var row in role.ModulePermissions)
        {
            var module = MongoObjectId.IsValid(row.ModuleId)
                ? await modules.GetByIdAsync(row.ModuleId, cancellationToken)
                : null;

            if (module is null)
            {
                return Results.BadRequest(
                    new { message = $"Module '{row.ModuleId}' was not found." });
            }

            var actions = ModuleActionList.Normalize(module.AvailableActions);

            row.ModuleName = module.ModuleName;
            row.View &= actions.Contains(ModuleActions.View);
            row.Create &= actions.Contains(ModuleActions.Create);
            row.Edit &= actions.Contains(ModuleActions.Edit);
            row.Delete &= actions.Contains(ModuleActions.Delete);
            row.Approve &= actions.Contains(ModuleActions.Approve);
            row.Export &= actions.Contains(ModuleActions.Export);

            if (row.HasAny())
            {
                resolved.Add(row);
            }
        }

        role.ModulePermissions = resolved;
        role.ModuleAccess = resolved
            .Select(x => new MasterDataReference { Id = x.ModuleId, Name = x.ModuleName })
            .ToList();

        return null;
    }

    private static async Task<RolePermissionMatrixResponse> BuildMatrixAsync(
        RoleEntity role,
        IModuleAccessRepository modules,
        CancellationToken cancellationToken)
    {
        var activeModules = await modules.SearchAsync(
            new ModuleAccessFilter(null, MasterDataStatuses.Active, null, role.ClientId),
            cancellationToken);

        var granted = role.ModulePermissions.ToDictionary(x => x.ModuleId);

        return new RolePermissionMatrixResponse(
            role.Id,
            role.RoleName,
            activeModules
                .Select(x => RoleContractDefaults.ToMatrixRow(x, granted.GetValueOrDefault(x.Id)))
                .ToList());
    }

    private static async Task<RoleResponse> ToResponseAsync(
        RoleEntity role,
        ISystemUserRepository users,
        CancellationToken cancellationToken)
    {
        var userCounts = await users.CountByPrimaryRoleAsync(
            [role.Id],
            cancellationToken);

        return RoleResponse.FromEntity(
            role,
            userCounts.GetValueOrDefault(role.Id));
    }

    private static async Task<string> NextFreeCodeAsync(
        string baseCode,
        string? clientId,
        IRoleRepository repository,
        CancellationToken cancellationToken)
    {
        var code = baseCode;

        for (var n = 2; await repository.GetByCodeAsync(code, clientId, cancellationToken) is not null; n++)
        {
            code = $"{baseCode}{n}";
        }

        return code;
    }

    private static CreateRoleRequest ToImportShape(RoleEntity role)
    {
        return new CreateRoleRequest(
            role.RoleName,
            role.Code,
            role.Type,
            role.Description,
            role.Permissions.Select(MasterDataReferenceDto.FromReference).ToList(),
            null,
            role.ModulePermissions
                .Select(x => new RoleModulePermissionDto(x.ModuleId, x.ModuleName, x.View, x.Create, x.Edit, x.Delete, x.Approve, x.Export))
                .ToList(),
            role.DataScope,
            role.Locations.Select(MasterDataReferenceDto.FromReference).ToList(),
            role.Departments.Select(MasterDataReferenceDto.FromReference).ToList(),
            role.ApprovalLimit,
            role.ApprovalCurrency,
            role.MfaRequired,
            null,
            role.Status,
            null,
            role.ClientId,
            role.TenantId);
    }

    private static IEnumerable<string> GrantedActions(RoleModulePermission x)
    {
        if (x.View) yield return ModuleActions.View;
        if (x.Create) yield return ModuleActions.Create;
        if (x.Edit) yield return ModuleActions.Edit;
        if (x.Delete) yield return ModuleActions.Delete;
        if (x.Approve) yield return ModuleActions.Approve;
        if (x.Export) yield return ModuleActions.Export;
    }

    private static MasterDataReference CopyReference(MasterDataReference x) => new() { Id = x.Id, Name = x.Name };

    private static RoleModulePermission CopyPermission(RoleModulePermission x) => new()
    {
        ModuleId = x.ModuleId,
        ModuleName = x.ModuleName,
        View = x.View,
        Create = x.Create,
        Edit = x.Edit,
        Delete = x.Delete,
        Approve = x.Approve,
        Export = x.Export
    };

    private static double Percent(int part, int total) => total == 0 ? 0 : Math.Round(part * 100.0 / total, 1);

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    // Import rows report errors as text; the result types used here all carry a message or validation errors.
    private static string Describe(IResult error)
    {
        return error switch
        {
            IValueHttpResult { Value: HttpValidationProblemDetails problem } =>
                string.Join("; ", problem.Errors.SelectMany(x => x.Value)),
            IValueHttpResult { Value: { } value } =>
                value.GetType().GetProperty("message")?.GetValue(value)?.ToString() ?? "Invalid row",
            _ => "Invalid row"
        };
    }
}
