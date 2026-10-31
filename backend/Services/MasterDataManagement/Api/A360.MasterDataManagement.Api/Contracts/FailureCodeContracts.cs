using FailureCodeEntity = A360.MasterDataManagement.Domain.Entities.FailureCode;

namespace A360.MasterDataManagement.Api.Contracts;

public static class FailureCodeSeverities
{
    public static readonly string[] All = ["Critical", "High", "Medium", "Low"];

    /// <summary>Returns the matching value in its standard spelling, or null when it is not one of <see cref="All"/>.</summary>
    public static string? Normalize(string? value)
    {
        return All.FirstOrDefault(x => x.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record CreateFailureCodeRequest(
    string? FailureCodeName,
    string? Code,
    string? Category,
    string? Severity,
    string? Description,
    List<string>? AssetCategories,
    List<string>? Causes,
    List<string>? Actions,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public FailureCodeEntity ToEntity()
    {
        return new FailureCodeEntity
        {
            FailureCodeName = FailureCodeName!.Trim(),
            Code = Code!.Trim(),
            Category = Category!.Trim(),
            Severity = FailureCodeSeverities.Normalize(Severity)!,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            AssetCategories = AssetCategories?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],
            Causes = Causes?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],
            Actions = Actions?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateFailureCodeRequest(
    string? FailureCodeName,
    string? Code,
    string? Category,
    string? Severity,
    string? Description,
    List<string>? AssetCategories,
    List<string>? Causes,
    List<string>? Actions,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(FailureCodeEntity failureCode)
    {
        failureCode.FailureCodeName = FailureCodeName!.Trim();
        failureCode.Code = Code!.Trim();
        failureCode.Category = Category!.Trim();
        failureCode.Severity = FailureCodeSeverities.Normalize(Severity)!;
        failureCode.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        failureCode.AssetCategories = AssetCategories?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        failureCode.Causes = Causes?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        failureCode.Actions = Actions?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];

        failureCode.UpdatedBy = UpdatedBy;
        failureCode.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            failureCode.Status = Status;
        }
    }
}

public sealed record UpdateFailureCodeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record FailureCodeResponse(
    string Id,
    string FailureCodeName,
    string Code,
    string Category,
    string Severity,
    string? Description,
    IReadOnlyList<string> AssetCategories,
    IReadOnlyList<string> Causes,
    IReadOnlyList<string> Actions,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static FailureCodeResponse FromEntity(FailureCodeEntity failureCode)
    {
        return new FailureCodeResponse(
            failureCode.Id ?? string.Empty,
            failureCode.FailureCodeName ?? string.Empty,
            failureCode.Code ?? string.Empty,
            failureCode.Category ?? string.Empty,
            failureCode.Severity ?? string.Empty,
            failureCode.Description,
            failureCode.AssetCategories ?? [],
            failureCode.Causes ?? [],
            failureCode.Actions ?? [],
            failureCode.CreatedBy,
            failureCode.CreatedAt,
            failureCode.UpdatedBy,
            failureCode.UpdatedAt,
            failureCode.ClientId,
            failureCode.TenantId,
            failureCode.Status,
            failureCode.IsDeleted
        );
    }
}
