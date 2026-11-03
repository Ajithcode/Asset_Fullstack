
using SparePartEntity = A360.Maintenance.Domain.Entities.SparePart;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateSparePartRequest(
    string? PartId,
    string? AssetId,
    string? AssetName,
    string? PartName,
    string? Category,
    int QuantityInStock,
    int MinimumStockLevel,
    double UnitCost,
    string? Supplier,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public SparePartEntity ToEntity()
    {
        return new SparePartEntity
        {
            PartId = PartId,
            AssetId = AssetId,
            AssetName = AssetName,
            PartName = PartName,
            Category = Category,
            QuantityInStock = QuantityInStock,
            MinimumStockLevel = MinimumStockLevel,
            UnitCost = UnitCost,
            Supplier = Supplier,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateSparePartRequest(
    string? PartId,
    string? AssetId,
    string? AssetName,
    string? PartName,
    string? Category,
    int QuantityInStock,
    int MinimumStockLevel,
    double UnitCost,
    string? Supplier,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(SparePartEntity sparePart)
    {
        sparePart.PartId = PartId;
        sparePart.AssetId = AssetId;
        sparePart.AssetName = AssetName;
        sparePart.PartName = PartName;
        sparePart.Category = Category;
        sparePart.QuantityInStock = QuantityInStock;
        sparePart.MinimumStockLevel = MinimumStockLevel;
        sparePart.UnitCost = UnitCost;
        sparePart.Supplier = Supplier;

        sparePart.UpdatedBy = UpdatedBy;
        sparePart.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            sparePart.Status = Status;
        }
    }
}

public sealed record SparePartResponse(
    string Id,
    string PartId,
    string? AssetId,
    string? AssetName,
    string PartName,
    string? Category,
    int QuantityInStock,
    int MinimumStockLevel,
    double UnitCost,
    string? Supplier,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static SparePartResponse FromEntity(SparePartEntity sparePart)
    {
        return new SparePartResponse(
            sparePart.Id ?? string.Empty,
            sparePart.PartId ?? string.Empty,
            sparePart.AssetId,
            sparePart.AssetName,
            sparePart.PartName ?? string.Empty,
            sparePart.Category,
            sparePart.QuantityInStock,
            sparePart.MinimumStockLevel,
            sparePart.UnitCost,
            sparePart.Supplier,
            sparePart.CreatedBy,
            sparePart.CreatedAt,
            sparePart.UpdatedBy,
            sparePart.UpdatedAt,
            sparePart.ClientId,
            sparePart.TenantId,
            sparePart.Status,
            sparePart.IsDeleted
        );
    }
}
