using ApiSyncStatusEntity = A360.MasterDataManagement.Domain.Entities.ApiSyncStatus;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateApiSyncStatusRequest(
    string? StatusName,
    string? Code,
    string? Description,
    string? Color,
    bool? IsSuccess,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ApiSyncStatusEntity ToEntity()
    {
        return new ApiSyncStatusEntity
        {
            StatusName = StatusName!.Trim(),
            Code = Code!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            IsSuccess = IsSuccess ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateApiSyncStatusRequest(
    string? StatusName,
    string? Code,
    string? Description,
    string? Color,
    bool? IsSuccess,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ApiSyncStatusEntity apiSyncStatus)
    {
        apiSyncStatus.StatusName = StatusName!.Trim();
        apiSyncStatus.Code = Code!.Trim();
        apiSyncStatus.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        apiSyncStatus.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        apiSyncStatus.IsSuccess = IsSuccess ?? false;

        apiSyncStatus.UpdatedBy = UpdatedBy;
        apiSyncStatus.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            apiSyncStatus.Status = Status;
        }
    }
}

public sealed record UpdateApiSyncStatusStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ApiSyncStatusResponse(
    string Id,
    string StatusName,
    string Code,
    string? Description,
    string? Color,
    bool IsSuccess,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ApiSyncStatusResponse FromEntity(ApiSyncStatusEntity apiSyncStatus)
    {
        return new ApiSyncStatusResponse(
            apiSyncStatus.Id ?? string.Empty,
            apiSyncStatus.StatusName ?? string.Empty,
            apiSyncStatus.Code ?? string.Empty,
            apiSyncStatus.Description,
            apiSyncStatus.Color,
            apiSyncStatus.IsSuccess,
            apiSyncStatus.CreatedBy,
            apiSyncStatus.CreatedAt,
            apiSyncStatus.UpdatedBy,
            apiSyncStatus.UpdatedAt,
            apiSyncStatus.ClientId,
            apiSyncStatus.TenantId,
            apiSyncStatus.Status,
            apiSyncStatus.IsDeleted
        );
    }
}
