
using MaterialConsumptionEntity = A360.Wip.Domain.Entities.MaterialConsumption;

namespace A360.Wip.Api.Contracts;

public sealed record CreateMaterialConsumptionRequest(
    string? MaterialId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    string? ItemName,
    string? ItemCode,
    double? QuantityPlanned,
    double? QuantityUsed,
    string? Unit,
    double? Cost,
    string? VendorId,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public MaterialConsumptionEntity ToEntity()
    {
        return new MaterialConsumptionEntity
        {
            MaterialId = MaterialId,
            AssetId = AssetId,
            AssetName = AssetName,
            JobId = JobId,
            TaskId = TaskId,

            ItemName = ItemName,
            ItemCode = ItemCode,

            QuantityPlanned = QuantityPlanned,
            QuantityUsed = QuantityUsed,
            Unit = Unit,
            Cost = Cost,
            VendorId = VendorId,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateMaterialConsumptionRequest(
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    string? ItemName,
    string? ItemCode,
    double? QuantityPlanned,
    double? QuantityUsed,
    string? Unit,
    double? Cost,
    string? VendorId,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(MaterialConsumptionEntity materialConsumption)
    {
        materialConsumption.AssetId = AssetId;
        materialConsumption.AssetName = AssetName;
        materialConsumption.JobId = JobId;
        materialConsumption.TaskId = TaskId;

        materialConsumption.ItemName = ItemName;
        materialConsumption.ItemCode = ItemCode;

        materialConsumption.QuantityPlanned = QuantityPlanned;
        materialConsumption.QuantityUsed = QuantityUsed;
        materialConsumption.Unit = Unit;
        materialConsumption.Cost = Cost;
        materialConsumption.VendorId = VendorId;

        materialConsumption.UpdatedBy = UpdatedBy;
        materialConsumption.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            materialConsumption.Status = Status;
        }
    }
}

public sealed record MaterialConsumptionResponse(
    string Id,
    string MaterialId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    string? ItemName,
    string? ItemCode,
    double? QuantityPlanned,
    double? QuantityUsed,
    string? Unit,
    double? Cost,
    string? VendorId,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static MaterialConsumptionResponse FromEntity(MaterialConsumptionEntity materialConsumption)
    {
        return new MaterialConsumptionResponse(
            materialConsumption.Id ?? string.Empty,
            materialConsumption.MaterialId ?? string.Empty,
            materialConsumption.AssetId,
            materialConsumption.AssetName,
            materialConsumption.JobId,
            materialConsumption.TaskId,

            materialConsumption.ItemName,
            materialConsumption.ItemCode,

            materialConsumption.QuantityPlanned,
            materialConsumption.QuantityUsed,
            materialConsumption.Unit,
            materialConsumption.Cost,
            materialConsumption.VendorId,

            materialConsumption.CreatedBy,
            materialConsumption.CreatedAt,
            materialConsumption.UpdatedBy,
            materialConsumption.UpdatedAt,

            materialConsumption.ClientId,
            materialConsumption.TenantId,
            materialConsumption.Status,
            materialConsumption.IsDeleted
        );
    }
}
