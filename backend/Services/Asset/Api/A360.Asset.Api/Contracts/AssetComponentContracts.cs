using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>Used for both create and update; the asset comes from the URL.</summary>
public sealed record SaveAssetComponentRequest(
    string? ParentComponentId,
    string? ComponentName,
    string? ComponentTag,
    string? Status,
    string? Model,
    string? Manufacturer,
    string? PartNumber,
    string? SerialNumber,
    DateTime? InstalledDate,
    DateTime? ExpectedReplacementDate,
    decimal? LifecycleUsed,
    decimal? LifecycleRatedLife,
    string? LifecycleUnit,
    string? Description,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public const string DefaultStatus = "Active";
    public const string DefaultLifecycleUnit = "Running hours";

    public AssetComponent ToEntity(string assetRegistrationId, CurrentUser? currentUser)
    {
        var component = new AssetComponent
        {
            AssetRegistrationId = assetRegistrationId,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        CopyFieldsTo(component);
        return component;
    }

    public void ApplyTo(AssetComponent component, CurrentUser? currentUser)
    {
        CopyFieldsTo(component);
        component.UpdatedBy = currentUser?.UserName;
        component.UpdatedAt = DateTime.UtcNow;
    }

    private void CopyFieldsTo(AssetComponent component)
    {
        component.ParentComponentId = ContractText.TrimOrNull(ParentComponentId);
        component.ComponentName = ComponentName!.Trim();
        component.ComponentTag = ComponentTag!.Trim();
        component.Status = ContractText.TrimOrNull(Status) ?? DefaultStatus;
        component.Model = ContractText.TrimOrNull(Model);
        component.Manufacturer = ContractText.TrimOrNull(Manufacturer);
        component.PartNumber = ContractText.TrimOrNull(PartNumber);
        component.SerialNumber = ContractText.TrimOrNull(SerialNumber);
        component.InstalledDate = ContractText.DateOnly(InstalledDate);
        component.ExpectedReplacementDate = ContractText.DateOnly(ExpectedReplacementDate);
        component.LifecycleUsed = LifecycleUsed ?? 0;
        component.LifecycleRatedLife = LifecycleRatedLife ?? 0;
        component.LifecycleUnit = ContractText.TrimOrNull(LifecycleUnit) ?? DefaultLifecycleUnit;
        component.Description = ContractText.TrimOrNull(Description);
    }
}

/// <param name="LifecyclePercent">Used as a percentage of rated life (e.g. 40 for 4,000 of 10,000); null when the rated life is 0.</param>
public sealed record AssetComponentResponse(
    string Id,
    string AssetRegistrationId,
    string? ParentComponentId,
    string? ParentComponentName,
    string ComponentName,
    string ComponentTag,
    string? Status,
    string? Model,
    string? Manufacturer,
    string? PartNumber,
    string? SerialNumber,
    DateTime? InstalledDate,
    DateTime? ExpectedReplacementDate,
    decimal LifecycleUsed,
    decimal LifecycleRatedLife,
    string? LifecycleUnit,
    decimal? LifecyclePercent,
    string? Description,
    int ChildCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetComponentResponse FromEntity(AssetComponent component, string? parentComponentName, int childCount)
    {
        return new AssetComponentResponse(
            component.Id,
            component.AssetRegistrationId,
            component.ParentComponentId,
            parentComponentName,
            component.ComponentName,
            component.ComponentTag,
            component.Status,
            component.Model,
            component.Manufacturer,
            component.PartNumber,
            component.SerialNumber,
            component.InstalledDate,
            component.ExpectedReplacementDate,
            component.LifecycleUsed,
            component.LifecycleRatedLife,
            component.LifecycleUnit,
            LifecyclePercentOf(component),
            component.Description,
            childCount,
            component.CreatedBy,
            component.CreatedAt,
            component.UpdatedBy,
            component.UpdatedAt,
            component.ClientId,
            component.TenantId);
    }

    public static decimal? LifecyclePercentOf(AssetComponent component)
    {
        return component.LifecycleRatedLife > 0
            ? decimal.Round(component.LifecycleUsed / component.LifecycleRatedLife * 100, 1, MidpointRounding.AwayFromZero)
            : null;
    }
}

public sealed record AssetComponentListResponse(
    IReadOnlyCollection<AssetComponentResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>A component with its sub-components nested, for the Component Structure tree.</summary>
public sealed record AssetComponentNodeResponse(
    string Id,
    string ComponentName,
    string ComponentTag,
    string? Status,
    decimal? LifecyclePercent,
    DateTime? ExpectedReplacementDate,
    IReadOnlyList<AssetComponentNodeResponse> Children);
