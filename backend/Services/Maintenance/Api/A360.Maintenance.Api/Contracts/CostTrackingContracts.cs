
using CostTrackingEntity = A360.Maintenance.Domain.Entities.CostTracking;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateCostTrackingRequest(
    string? AssetId,
    string? AssetName,
    double LaborCost,
    double SparePartsCost,
    double TotalMaintenanceCost,
    double BudgetAllocation,
    double CostPerAsset,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public CostTrackingEntity ToEntity()
    {
        return new CostTrackingEntity
        {
            AssetId = AssetId,
            AssetName = AssetName,
            LaborCost = LaborCost,
            SparePartsCost = SparePartsCost,
            TotalMaintenanceCost = TotalMaintenanceCost,
            BudgetAllocation = BudgetAllocation,
            CostPerAsset = CostPerAsset,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateCostTrackingRequest(
    string? AssetId,
    string? AssetName,
    double LaborCost,
    double SparePartsCost,
    double TotalMaintenanceCost,
    double BudgetAllocation,
    double CostPerAsset,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(CostTrackingEntity costTracking)
    {
        costTracking.AssetId = AssetId;
        costTracking.AssetName = AssetName;
        costTracking.LaborCost = LaborCost;
        costTracking.SparePartsCost = SparePartsCost;
        costTracking.TotalMaintenanceCost = TotalMaintenanceCost;
        costTracking.BudgetAllocation = BudgetAllocation;
        costTracking.CostPerAsset = CostPerAsset;

        costTracking.UpdatedBy = UpdatedBy;
        costTracking.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            costTracking.Status = Status;
        }
    }
}

public sealed record CostTrackingResponse(
    string Id,
    string AssetId,
    string AssetName,
    double LaborCost,
    double SparePartsCost,
    double TotalMaintenanceCost,
    double BudgetAllocation,
    double CostPerAsset,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static CostTrackingResponse FromEntity(CostTrackingEntity costTracking)
    {
        return new CostTrackingResponse(
            costTracking.Id ?? string.Empty,
            costTracking.AssetId ?? string.Empty,
            costTracking.AssetName ?? string.Empty,
            costTracking.LaborCost,
            costTracking.SparePartsCost,
            costTracking.TotalMaintenanceCost,
            costTracking.BudgetAllocation,
            costTracking.CostPerAsset,
            costTracking.CreatedBy,
            costTracking.CreatedAt,
            costTracking.UpdatedBy,
            costTracking.UpdatedAt,
            costTracking.ClientId,
            costTracking.TenantId,
            costTracking.Status,
            costTracking.IsDeleted
        );
    }
}
