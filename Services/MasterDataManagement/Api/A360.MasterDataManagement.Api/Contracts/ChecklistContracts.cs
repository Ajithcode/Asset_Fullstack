using ChecklistEntity = A360.MasterDataManagement.Domain.Entities.Checklist;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateChecklistRequest(
    string? ChecklistName,
    string? Code,
    string? ChecklistType,
    string? Category,
    string? Description,
    List<MasterDataStepDto>? Items,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ChecklistEntity ToEntity()
    {
        return new ChecklistEntity
        {
            ChecklistName = ChecklistName!.Trim(),
            Code = Code!.Trim(),
            ChecklistType = ChecklistType!.Trim(),
            Category = Category!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Items = Items?.Select(x => x.ToStep()).ToList() ?? [],

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateChecklistRequest(
    string? ChecklistName,
    string? Code,
    string? ChecklistType,
    string? Category,
    string? Description,
    List<MasterDataStepDto>? Items,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ChecklistEntity checklist)
    {
        checklist.ChecklistName = ChecklistName!.Trim();
        checklist.Code = Code!.Trim();
        checklist.ChecklistType = ChecklistType!.Trim();
        checklist.Category = Category!.Trim();
        checklist.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        checklist.Items = Items?.Select(x => x.ToStep()).ToList() ?? [];

        checklist.UpdatedBy = UpdatedBy;
        checklist.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            checklist.Status = Status;
        }
    }
}

public sealed record UpdateChecklistStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ChecklistResponse(
    string Id,
    string ChecklistName,
    string Code,
    string ChecklistType,
    string Category,
    string? Description,
    IReadOnlyList<MasterDataStepDto> Items,
    int ItemCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ChecklistResponse FromEntity(ChecklistEntity checklist)
    {
        var items = (checklist.Items ?? []).Select(MasterDataStepDto.FromStep).ToList();

        return new ChecklistResponse(
            checklist.Id ?? string.Empty,
            checklist.ChecklistName ?? string.Empty,
            checklist.Code ?? string.Empty,
            checklist.ChecklistType ?? string.Empty,
            checklist.Category ?? string.Empty,
            checklist.Description,
            items,
            items.Count,
            checklist.CreatedBy,
            checklist.CreatedAt,
            checklist.UpdatedBy,
            checklist.UpdatedAt,
            checklist.ClientId,
            checklist.TenantId,
            checklist.Status,
            checklist.IsDeleted
        );
    }
}
