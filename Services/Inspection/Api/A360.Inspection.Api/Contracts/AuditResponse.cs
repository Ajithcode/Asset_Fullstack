using A360.Domain.Entities;

namespace A360.Inspection.Api.Contracts;

/// <summary>Audit fields every Inspection response carries (see InspectionAudit in the frontend models).</summary>
public abstract record AuditResponse
{
    protected AuditResponse(BaseEntity entity)
    {
        Id = entity.Id;
        Status = entity.Status;
        CreatedBy = entity.CreatedBy;
        CreatedAt = entity.CreatedAt;
        UpdatedBy = entity.UpdatedBy;
        UpdatedAt = entity.UpdatedAt;
        ClientId = entity.ClientId;
        TenantId = entity.TenantId;
        IsDeleted = entity.IsDeleted;
    }

    public string Id { get; init; }

    public string? Status { get; init; }

    public string? CreatedBy { get; init; }

    public DateTime? CreatedAt { get; init; }

    public string? UpdatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public bool IsDeleted { get; init; }
}
