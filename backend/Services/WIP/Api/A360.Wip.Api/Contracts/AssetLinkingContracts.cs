
using AssetLinkingEntity = A360.Wip.Domain.Entities.AssetLinking;

namespace A360.Wip.Api.Contracts;

public sealed record CreateAssetLinkingRequest(
    string? AssetId,
    string? AssetName,
    string? AssetType,
    string? SerialNumber,
    string? RfidTag,
    string? IotDeviceId,
    string? CurrentStatus,
    string? UtilizationStatus,
    DateTime? LastMaintenanceDate,
    string? Condition,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public AssetLinkingEntity ToEntity()
    {
        return new AssetLinkingEntity
        {
            AssetId = AssetId,
            AssetName = AssetName,
            AssetType = AssetType,

            SerialNumber = SerialNumber,
            RfidTag = RfidTag,
            IotDeviceId = IotDeviceId,

            CurrentStatus = CurrentStatus,
            UtilizationStatus = UtilizationStatus,
            LastMaintenanceDate = LastMaintenanceDate,
            Condition = Condition,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetLinkingRequest(
    string? AssetName,
    string? AssetType,
    string? SerialNumber,
    string? RfidTag,
    string? IotDeviceId,
    string? CurrentStatus,
    string? UtilizationStatus,
    DateTime? LastMaintenanceDate,
    string? Condition,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(AssetLinkingEntity assetLinking)
    {
        assetLinking.AssetName = AssetName;
        assetLinking.AssetType = AssetType;

        assetLinking.SerialNumber = SerialNumber;
        assetLinking.RfidTag = RfidTag;
        assetLinking.IotDeviceId = IotDeviceId;

        assetLinking.CurrentStatus = CurrentStatus;
        assetLinking.UtilizationStatus = UtilizationStatus;
        assetLinking.LastMaintenanceDate = LastMaintenanceDate;
        assetLinking.Condition = Condition;

        assetLinking.UpdatedBy = UpdatedBy;
        assetLinking.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            assetLinking.Status = Status;
        }
    }
}

public sealed record AssetLinkingResponse(
    string Id,
    string AssetId,
    string? AssetName,
    string? AssetType,
    string? SerialNumber,
    string? RfidTag,
    string? IotDeviceId,
    string? CurrentStatus,
    string? UtilizationStatus,
    DateTime? LastMaintenanceDate,
    string? Condition,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AssetLinkingResponse FromEntity(AssetLinkingEntity assetLinking)
    {
        return new AssetLinkingResponse(
            assetLinking.Id ?? string.Empty,
            assetLinking.AssetId ?? string.Empty,
            assetLinking.AssetName,
            assetLinking.AssetType,

            assetLinking.SerialNumber,
            assetLinking.RfidTag,
            assetLinking.IotDeviceId,

            assetLinking.CurrentStatus,
            assetLinking.UtilizationStatus,
            assetLinking.LastMaintenanceDate,
            assetLinking.Condition,

            assetLinking.CreatedBy,
            assetLinking.CreatedAt,
            assetLinking.UpdatedBy,
            assetLinking.UpdatedAt,

            assetLinking.ClientId,
            assetLinking.TenantId,
            assetLinking.Status,
            assetLinking.IsDeleted
        );
    }
}
