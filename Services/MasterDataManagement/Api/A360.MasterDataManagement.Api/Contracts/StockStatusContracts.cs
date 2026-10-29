using StockStatusEntity = A360.MasterDataManagement.Domain.Entities.StockStatus;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateStockStatusRequest(
    string? StatusName,
    string? Code,
    string? Type,
    string? Color,
    string? Description,
    string? Usage,
    bool? SystemDefault,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public StockStatusEntity ToEntity()
    {
        return new StockStatusEntity
        {
            StatusName = StatusName!.Trim(),
            Code = Code!.Trim(),
            Type = Type!.Trim(),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Usage = string.IsNullOrWhiteSpace(Usage) ? null : Usage.Trim(),
            SystemDefault = SystemDefault ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateStockStatusRequest(
    string? StatusName,
    string? Code,
    string? Type,
    string? Color,
    string? Description,
    string? Usage,
    bool? SystemDefault,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(StockStatusEntity stockStatus)
    {
        stockStatus.StatusName = StatusName!.Trim();
        stockStatus.Code = Code!.Trim();
        stockStatus.Type = Type!.Trim();
        stockStatus.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        stockStatus.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        stockStatus.Usage = string.IsNullOrWhiteSpace(Usage) ? null : Usage.Trim();
        stockStatus.SystemDefault = SystemDefault ?? false;

        stockStatus.UpdatedBy = UpdatedBy;
        stockStatus.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            stockStatus.Status = Status;
        }
    }
}

public sealed record UpdateStockStatusStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record StockStatusResponse(
    string Id,
    string StatusName,
    string Code,
    string Type,
    string? Color,
    string? Description,
    string? Usage,
    bool SystemDefault,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static StockStatusResponse FromEntity(StockStatusEntity stockStatus)
    {
        return new StockStatusResponse(
            stockStatus.Id ?? string.Empty,
            stockStatus.StatusName ?? string.Empty,
            stockStatus.Code ?? string.Empty,
            stockStatus.Type ?? string.Empty,
            stockStatus.Color,
            stockStatus.Description,
            stockStatus.Usage,
            stockStatus.SystemDefault,
            stockStatus.CreatedBy,
            stockStatus.CreatedAt,
            stockStatus.UpdatedBy,
            stockStatus.UpdatedAt,
            stockStatus.ClientId,
            stockStatus.TenantId,
            stockStatus.Status,
            stockStatus.IsDeleted
        );
    }
}
