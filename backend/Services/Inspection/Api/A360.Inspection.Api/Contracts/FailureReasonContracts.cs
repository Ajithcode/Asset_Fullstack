using FailureReasonEntity = A360.Inspection.Domain.Entities.FailureReason;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateFailureReasonRequest(
    string? FailureReasonName,
    string? FailureCategory,
    string? Severity,
    bool? IsCorrectiveActionRequired,
    bool? IsEscalationRequired,
    string? DefaultResponsibleTeam,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public FailureReasonEntity ToEntity(string failureReasonCode)
    {
        return new FailureReasonEntity
        {
            FailureReasonCode = failureReasonCode,
            FailureReasonName = FailureReasonName ?? string.Empty,
            FailureCategory = FailureCategory ?? string.Empty,
            Severity = Severity ?? string.Empty,
            IsCorrectiveActionRequired = IsCorrectiveActionRequired ?? false,
            IsEscalationRequired = IsEscalationRequired ?? false,
            DefaultResponsibleTeam = DefaultResponsibleTeam ?? string.Empty,
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

public sealed record UpdateFailureReasonRequest(
    string? FailureReasonName,
    string? FailureCategory,
    string? Severity,
    bool? IsCorrectiveActionRequired,
    bool? IsEscalationRequired,
    string? DefaultResponsibleTeam,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(FailureReasonEntity failureReason)
    {
        failureReason.FailureReasonName = FailureReasonName ?? string.Empty;
        failureReason.FailureCategory = FailureCategory ?? string.Empty;
        failureReason.Severity = Severity ?? string.Empty;
        failureReason.IsCorrectiveActionRequired = IsCorrectiveActionRequired ?? failureReason.IsCorrectiveActionRequired;
        failureReason.IsEscalationRequired = IsEscalationRequired ?? failureReason.IsEscalationRequired;
        failureReason.DefaultResponsibleTeam = DefaultResponsibleTeam ?? string.Empty;
        failureReason.IsActive = IsActive ?? failureReason.IsActive;
        failureReason.Status = Status;
        failureReason.UpdatedBy = UpdatedBy;
        failureReason.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record FailureReasonResponse(
    string Id,
    string FailureReasonCode,
    string FailureReasonName,
    string FailureCategory,
    string Severity,
    bool IsCorrectiveActionRequired,
    bool IsEscalationRequired,
    string DefaultResponsibleTeam,
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
    public static FailureReasonResponse FromEntity(FailureReasonEntity failureReason)
    {
        return new FailureReasonResponse(
            failureReason.Id,
            failureReason.FailureReasonCode,
            failureReason.FailureReasonName,
            failureReason.FailureCategory,
            failureReason.Severity,
            failureReason.IsCorrectiveActionRequired,
            failureReason.IsEscalationRequired,
            failureReason.DefaultResponsibleTeam,
            failureReason.IsActive,
            failureReason.Status,
            failureReason.CreatedBy,
            failureReason.CreatedAt,
            failureReason.UpdatedBy,
            failureReason.UpdatedAt,
            failureReason.ClientId,
            failureReason.TenantId,
            failureReason.IsDeleted);
    }
}
