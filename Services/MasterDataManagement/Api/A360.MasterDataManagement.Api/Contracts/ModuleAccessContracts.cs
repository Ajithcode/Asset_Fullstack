using ModuleAccessEntity = A360.MasterDataManagement.Domain.Entities.ModuleAccess;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateModuleAccessRequest(
    string? ModuleName,
    string? Code,
    string? Category,
    string? Description,
    string? Icon,
    List<string>? AvailableActions,
    int? SortOrder,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ModuleAccessEntity ToEntity()
    {
        return new ModuleAccessEntity
        {
            ModuleName = ModuleName!.Trim(),
            Code = Code!.Trim(),
            Category = Category!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),
            AvailableActions = ModuleActionList.Normalize(AvailableActions),
            SortOrder = SortOrder ?? 0,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateModuleAccessRequest(
    string? ModuleName,
    string? Code,
    string? Category,
    string? Description,
    string? Icon,
    List<string>? AvailableActions,
    int? SortOrder,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ModuleAccessEntity moduleAccess)
    {
        moduleAccess.ModuleName = ModuleName!.Trim();
        moduleAccess.Code = Code!.Trim();
        moduleAccess.Category = Category!.Trim();
        moduleAccess.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        moduleAccess.Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();
        moduleAccess.AvailableActions = ModuleActionList.Normalize(AvailableActions);

        if (SortOrder is not null)
        {
            moduleAccess.SortOrder = SortOrder.Value;
        }

        moduleAccess.UpdatedBy = UpdatedBy;
        moduleAccess.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            moduleAccess.Status = Status;
        }
    }
}

public sealed record UpdateModuleAccessStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ModuleAccessResponse(
    string Id,
    string ModuleName,
    string Code,
    string Category,
    string? Description,
    string? Icon,
    IReadOnlyList<string> AvailableActions,
    int SortOrder,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ModuleAccessResponse FromEntity(ModuleAccessEntity moduleAccess)
    {
        return new ModuleAccessResponse(
            moduleAccess.Id ?? string.Empty,
            moduleAccess.ModuleName ?? string.Empty,
            moduleAccess.Code ?? string.Empty,
            moduleAccess.Category ?? string.Empty,
            moduleAccess.Description,
            moduleAccess.Icon,
            ModuleActionList.Normalize(moduleAccess.AvailableActions),
            moduleAccess.SortOrder,
            moduleAccess.CreatedBy,
            moduleAccess.CreatedAt,
            moduleAccess.UpdatedBy,
            moduleAccess.UpdatedAt,
            moduleAccess.ClientId,
            moduleAccess.TenantId,
            moduleAccess.Status,
            moduleAccess.IsDeleted
        );
    }
}

public static class ModuleActionList
{
    /// <summary>
    /// Returns the actions in matrix column order with their canonical casing.
    /// Null or empty means every action applies.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string>? actions)
    {
        var given = actions?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (given is null || given.Count == 0)
        {
            return [.. ModuleActions.All];
        }

        return ModuleActions.All.Where(given.Contains).ToList();
    }

    public static IReadOnlyList<string> Unknown(IEnumerable<string>? actions)
    {
        return actions?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !ModuleActions.All.Contains(x.Trim(), StringComparer.OrdinalIgnoreCase))
            .ToList() ?? [];
    }
}

public sealed record SeedDefaultModulesRequest(
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record SeedDefaultModulesResponse(
    int Created,
    int Skipped,
    IReadOnlyList<ModuleAccessResponse> Modules);
