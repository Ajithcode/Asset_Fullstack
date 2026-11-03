using SeverityEntity = A360.Inspection.Domain.Entities.Severity;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateSeverityRequest(
    string? SeverityName,
    int? Score,
    string? ColourIndicator,
    string? ResolutionSla,
    string? EscalationLevel,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public SeverityEntity ToEntity(string severityCode)
    {
        return new SeverityEntity
        {
            SeverityCode = severityCode,
            SeverityName = SeverityName ?? string.Empty,
            Score = Score ?? 0,
            ColourIndicator = ColourIndicator ?? string.Empty,
            ResolutionSla = ResolutionSla ?? string.Empty,
            EscalationLevel = EscalationLevel ?? string.Empty,
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

public sealed record UpdateSeverityRequest(
    string? SeverityName,
    int? Score,
    string? ColourIndicator,
    string? ResolutionSla,
    string? EscalationLevel,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(SeverityEntity severity)
    {
        severity.SeverityName = SeverityName ?? string.Empty;
        severity.Score = Score ?? severity.Score;
        severity.ColourIndicator = ColourIndicator ?? string.Empty;
        severity.ResolutionSla = ResolutionSla ?? string.Empty;
        severity.EscalationLevel = EscalationLevel ?? string.Empty;
        severity.IsActive = IsActive ?? severity.IsActive;
        severity.Status = Status;
        severity.UpdatedBy = UpdatedBy;
        severity.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record SeverityResponse(
    string Id,
    string SeverityCode,
    string SeverityName,
    int Score,
    string ColourIndicator,
    string ResolutionSla,
    string EscalationLevel,
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
    public static SeverityResponse FromEntity(SeverityEntity severity)
    {
        return new SeverityResponse(
            severity.Id,
            severity.SeverityCode,
            severity.SeverityName,
            severity.Score,
            severity.ColourIndicator,
            severity.ResolutionSla,
            severity.EscalationLevel,
            severity.IsActive,
            severity.Status,
            severity.CreatedBy,
            severity.CreatedAt,
            severity.UpdatedBy,
            severity.UpdatedAt,
            severity.ClientId,
            severity.TenantId,
            severity.IsDeleted);
    }
}
