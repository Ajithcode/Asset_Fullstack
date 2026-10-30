using PrioritySeverityEntity = A360.MasterDataManagement.Domain.Entities.PrioritySeverity;

namespace A360.MasterDataManagement.Api.Contracts;

public static class PrioritySeverityTypes
{
    public static readonly string[] All = ["Priority", "Severity"];

    /// <summary>Returns the matching value in its standard spelling, or null when it is not one of <see cref="All"/>.</summary>
    public static string? Normalize(string? value)
    {
        return All.FirstOrDefault(x => x.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record CreatePrioritySeverityRequest(
    string? Name,
    string? Code,
    string? Type,
    string? Color,
    string? ResponseTime,
    string? Description,
    List<string>? AppliesTo,
    int? EscalateAfterHours,
    bool? NotifyManager,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public PrioritySeverityEntity ToEntity()
    {
        return new PrioritySeverityEntity
        {
            Name = Name!.Trim(),
            Code = Code!.Trim(),
            Type = PrioritySeverityTypes.Normalize(Type)!,
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            ResponseTime = string.IsNullOrWhiteSpace(ResponseTime) ? null : ResponseTime.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            AppliesTo = AppliesTo?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],
            EscalateAfterHours = EscalateAfterHours,
            NotifyManager = NotifyManager ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePrioritySeverityRequest(
    string? Name,
    string? Code,
    string? Type,
    string? Color,
    string? ResponseTime,
    string? Description,
    List<string>? AppliesTo,
    int? EscalateAfterHours,
    bool? NotifyManager,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(PrioritySeverityEntity prioritySeverity)
    {
        prioritySeverity.Name = Name!.Trim();
        prioritySeverity.Code = Code!.Trim();
        prioritySeverity.Type = PrioritySeverityTypes.Normalize(Type)!;
        prioritySeverity.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        prioritySeverity.ResponseTime = string.IsNullOrWhiteSpace(ResponseTime) ? null : ResponseTime.Trim();
        prioritySeverity.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        prioritySeverity.AppliesTo = AppliesTo?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        prioritySeverity.EscalateAfterHours = EscalateAfterHours;
        prioritySeverity.NotifyManager = NotifyManager ?? false;

        prioritySeverity.UpdatedBy = UpdatedBy;
        prioritySeverity.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            prioritySeverity.Status = Status;
        }
    }
}

public sealed record UpdatePrioritySeverityStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record PrioritySeverityResponse(
    string Id,
    string Name,
    string Code,
    string Type,
    string? Color,
    string? ResponseTime,
    string? Description,
    IReadOnlyList<string> AppliesTo,
    int? EscalateAfterHours,
    bool NotifyManager,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static PrioritySeverityResponse FromEntity(PrioritySeverityEntity prioritySeverity)
    {
        return new PrioritySeverityResponse(
            prioritySeverity.Id ?? string.Empty,
            prioritySeverity.Name ?? string.Empty,
            prioritySeverity.Code ?? string.Empty,
            prioritySeverity.Type ?? string.Empty,
            prioritySeverity.Color,
            prioritySeverity.ResponseTime,
            prioritySeverity.Description,
            prioritySeverity.AppliesTo ?? [],
            prioritySeverity.EscalateAfterHours,
            prioritySeverity.NotifyManager,
            prioritySeverity.CreatedBy,
            prioritySeverity.CreatedAt,
            prioritySeverity.UpdatedBy,
            prioritySeverity.UpdatedAt,
            prioritySeverity.ClientId,
            prioritySeverity.TenantId,
            prioritySeverity.Status,
            prioritySeverity.IsDeleted
        );
    }
}
