using PriorityEntity = A360.Inspection.Domain.Entities.Priority;

namespace A360.Inspection.Api.Contracts;

public sealed record CreatePriorityRequest(
    string? PriorityName,
    string? ResponseTime,
    string? CompletionSla,
    string? Colour,
    string? EscalationRule,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public PriorityEntity ToEntity(string priorityCode)
    {
        return new PriorityEntity
        {
            PriorityCode = priorityCode,
            PriorityName = PriorityName ?? string.Empty,
            ResponseTime = ResponseTime ?? string.Empty,
            CompletionSla = CompletionSla ?? string.Empty,
            Colour = Colour ?? string.Empty,
            EscalationRule = EscalationRule ?? string.Empty,
            IsActive = IsActive ?? true,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePriorityRequest(
    string? PriorityName,
    string? ResponseTime,
    string? CompletionSla,
    string? Colour,
    string? EscalationRule,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(PriorityEntity priority)
    {
        priority.PriorityName = PriorityName ?? string.Empty;
        priority.ResponseTime = ResponseTime ?? string.Empty;
        priority.CompletionSla = CompletionSla ?? string.Empty;
        priority.Colour = Colour ?? string.Empty;
        priority.EscalationRule = EscalationRule ?? string.Empty;
        priority.IsActive = IsActive ?? priority.IsActive;
        priority.Status = Status;
        priority.UpdatedBy = UpdatedBy;
        priority.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record PriorityResponse(
    string Id,
    string PriorityCode,
    string PriorityName,
    string ResponseTime,
    string CompletionSla,
    string Colour,
    string EscalationRule,
    bool IsActive,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static PriorityResponse FromEntity(PriorityEntity priority)
    {
        return new PriorityResponse(
            priority.Id,
            priority.PriorityCode,
            priority.PriorityName,
            priority.ResponseTime,
            priority.CompletionSla,
            priority.Colour,
            priority.EscalationRule,
            priority.IsActive,
            priority.Status,
            priority.CreatedBy,
            priority.CreatedAt,
            priority.UpdatedBy,
            priority.UpdatedAt,
            priority.ClientId,
            priority.TenantId,
            priority.IsDeleted);
    }
}
