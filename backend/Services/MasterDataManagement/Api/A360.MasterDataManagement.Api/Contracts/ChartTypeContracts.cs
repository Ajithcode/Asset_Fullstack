using ChartTypeEntity = A360.MasterDataManagement.Domain.Entities.ChartType;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateChartTypeRequest(
    string? ChartTypeName,
    string? Code,
    string? Category,
    string? Description,
    string? IconUrl,
    string? PreviewType,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ChartTypeEntity ToEntity()
    {
        return new ChartTypeEntity
        {
            ChartTypeName = ChartTypeName!.Trim(),
            Code = Code!.Trim(),
            Category = Category!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(IconUrl) ? null : IconUrl.Trim(),
            PreviewType = string.IsNullOrWhiteSpace(PreviewType) ? null : PreviewType.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateChartTypeRequest(
    string? ChartTypeName,
    string? Code,
    string? Category,
    string? Description,
    string? IconUrl,
    string? PreviewType,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ChartTypeEntity chartType)
    {
        chartType.ChartTypeName = ChartTypeName!.Trim();
        chartType.Code = Code!.Trim();
        chartType.Category = Category!.Trim();
        chartType.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        chartType.IconUrl = string.IsNullOrWhiteSpace(IconUrl) ? null : IconUrl.Trim();
        chartType.PreviewType = string.IsNullOrWhiteSpace(PreviewType) ? null : PreviewType.Trim();

        chartType.UpdatedBy = UpdatedBy;
        chartType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            chartType.Status = Status;
        }
    }
}

public sealed record UpdateChartTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ChartTypeResponse(
    string Id,
    string ChartTypeName,
    string Code,
    string Category,
    string? Description,
    string? IconUrl,
    string? PreviewType,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ChartTypeResponse FromEntity(ChartTypeEntity chartType)
    {
        return new ChartTypeResponse(
            chartType.Id ?? string.Empty,
            chartType.ChartTypeName ?? string.Empty,
            chartType.Code ?? string.Empty,
            chartType.Category ?? string.Empty,
            chartType.Description,
            chartType.IconUrl,
            chartType.PreviewType,
            chartType.CreatedBy,
            chartType.CreatedAt,
            chartType.UpdatedBy,
            chartType.UpdatedAt,
            chartType.ClientId,
            chartType.TenantId,
            chartType.Status,
            chartType.IsDeleted
        );
    }
}
