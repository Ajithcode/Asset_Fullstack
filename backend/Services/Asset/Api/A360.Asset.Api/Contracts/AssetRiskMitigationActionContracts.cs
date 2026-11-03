using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>Add Mitigation Action form.</summary>
public sealed record CreateAssetRiskMitigationActionRequest(
    string? AssetRegistrationId,
    string? Action,
    string? Owner,
    DateTime? DueDate,
    string? Status,
    string? ExpectedCriticality,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetRiskMitigationAction ToEntity(AssetRegistration asset, CurrentUser? currentUser)
    {
        var status = AssetRiskMitigationStatuses.Normalize(Status) ?? AssetRiskMitigationStatuses.Open;

        return new AssetRiskMitigationAction
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            AssetName = asset.BasicInformation.AssetName,
            AssetTag = asset.BasicInformation.AssetTag,
            Action = Action!.Trim(),
            Owner = Owner!.Trim(),
            DueDate = ContractText.DateOnly(DueDate)!.Value,
            Status = status,
            ExpectedCriticality = AssetCriticalityLevels.Normalize(ExpectedCriticality),
            CompletedAt = status == AssetRiskMitigationStatuses.Completed ? DateTime.UtcNow : null,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

/// <summary>The Status dropdown in the Risk Mitigation Actions list.</summary>
public sealed record UpdateAssetRiskMitigationActionStatusRequest(string? Status)
{
    public void ApplyTo(AssetRiskMitigationAction action, CurrentUser? currentUser)
    {
        var status = AssetRiskMitigationStatuses.Normalize(Status)!;
        if (status == AssetRiskMitigationStatuses.Completed && action.Status != AssetRiskMitigationStatuses.Completed)
        {
            action.CompletedAt = DateTime.UtcNow;
        }
        else if (status != AssetRiskMitigationStatuses.Completed)
        {
            action.CompletedAt = null;
        }

        action.Status = status;
        action.UpdatedBy = currentUser?.UserName;
        action.UpdatedAt = DateTime.UtcNow;
    }
}

public static class AssetRiskMitigationStatuses
{
    public const string Open = "Open";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static IEnumerable<string> All => [Open, InProgress, Completed, Cancelled];

    public static bool IsValid(string? status) =>
        All.Any(value => string.Equals(value, status?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? status) =>
        All.FirstOrDefault(value => string.Equals(value, status?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <param name="IsOverdue">Due date has passed and the action is not Completed or Cancelled.</param>
public sealed record AssetRiskMitigationActionResponse(
    string Id,
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    string Action,
    string Owner,
    DateTime DueDate,
    bool IsOverdue,
    string? Status,
    string? ExpectedCriticality,
    DateTime? CompletedAt,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetRiskMitigationActionResponse FromEntity(AssetRiskMitigationAction action)
    {
        var isClosed = action.Status is AssetRiskMitigationStatuses.Completed or AssetRiskMitigationStatuses.Cancelled;

        return new AssetRiskMitigationActionResponse(
            action.Id,
            action.AssetRegistrationId,
            action.AssetName,
            action.AssetTag,
            action.Action,
            action.Owner,
            action.DueDate,
            !isClosed && action.DueDate < DateTime.UtcNow.Date,
            action.Status,
            action.ExpectedCriticality,
            action.CompletedAt,
            action.CreatedBy,
            action.CreatedAt,
            action.UpdatedBy,
            action.UpdatedAt,
            action.ClientId,
            action.TenantId);
    }
}

/// <param name="OpenCount">For the "(3 open)" header.</param>
public sealed record AssetRiskMitigationActionListResponse(
    IReadOnlyCollection<AssetRiskMitigationActionResponse> Items,
    long TotalCount,
    long OpenCount,
    int Page,
    int PageSize);
