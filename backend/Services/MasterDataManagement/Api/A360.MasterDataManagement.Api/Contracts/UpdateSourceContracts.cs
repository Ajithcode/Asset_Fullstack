using UpdateSourceEntity = A360.MasterDataManagement.Domain.Entities.UpdateSource;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateUpdateSourceRequest(
    string? SourceName,
    string? Code,
    string? Type,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public UpdateSourceEntity ToEntity()
    {
        return new UpdateSourceEntity
        {
            SourceName = SourceName!.Trim(),
            Code = Code!.Trim(),
            Type = Type!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateUpdateSourceRequest(
    string? SourceName,
    string? Code,
    string? Type,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(UpdateSourceEntity updateSource)
    {
        updateSource.SourceName = SourceName!.Trim();
        updateSource.Code = Code!.Trim();
        updateSource.Type = Type!.Trim();
        updateSource.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        updateSource.UpdatedBy = UpdatedBy;
        updateSource.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            updateSource.Status = Status;
        }
    }
}

public sealed record UpdateUpdateSourceStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record UpdateSourceResponse(
    string Id,
    string SourceName,
    string Code,
    string Type,
    string? Description,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static UpdateSourceResponse FromEntity(UpdateSourceEntity updateSource)
    {
        return new UpdateSourceResponse(
            updateSource.Id ?? string.Empty,
            updateSource.SourceName ?? string.Empty,
            updateSource.Code ?? string.Empty,
            updateSource.Type ?? string.Empty,
            updateSource.Description,
            updateSource.CreatedBy,
            updateSource.CreatedAt,
            updateSource.UpdatedBy,
            updateSource.UpdatedAt,
            updateSource.ClientId,
            updateSource.TenantId,
            updateSource.Status,
            updateSource.IsDeleted
        );
    }
}
