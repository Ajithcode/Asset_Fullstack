using DefectEntity = A360.Inspection.Domain.Entities.Defect;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateDefectRequest(
    string? DefectName,
    string? DefectCategory,
    string? Description,
    string? Severity,
    string? RiskRating,
    string? RecommendedCorrectiveAction,
    string? DefaultResolutionPeriod,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public DefectEntity ToEntity(string defectCode)
    {
        return new DefectEntity
        {
            DefectCode = defectCode,
            DefectName = DefectName ?? string.Empty,
            DefectCategory = DefectCategory ?? string.Empty,
            Description = Description ?? string.Empty,
            Severity = Severity ?? string.Empty,
            RiskRating = RiskRating ?? string.Empty,
            RecommendedCorrectiveAction = RecommendedCorrectiveAction ?? string.Empty,
            DefaultResolutionPeriod = DefaultResolutionPeriod ?? string.Empty,
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

public sealed record UpdateDefectRequest(
    string? DefectName,
    string? DefectCategory,
    string? Description,
    string? Severity,
    string? RiskRating,
    string? RecommendedCorrectiveAction,
    string? DefaultResolutionPeriod,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(DefectEntity defect)
    {
        defect.DefectName = DefectName ?? string.Empty;
        defect.DefectCategory = DefectCategory ?? string.Empty;
        defect.Description = Description ?? string.Empty;
        defect.Severity = Severity ?? string.Empty;
        defect.RiskRating = RiskRating ?? string.Empty;
        defect.RecommendedCorrectiveAction = RecommendedCorrectiveAction ?? string.Empty;
        defect.DefaultResolutionPeriod = DefaultResolutionPeriod ?? string.Empty;
        defect.IsActive = IsActive ?? defect.IsActive;
        defect.Status = Status;
        defect.UpdatedBy = UpdatedBy;
        defect.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record DefectResponse(
    string Id,
    string DefectCode,
    string DefectName,
    string DefectCategory,
    string Description,
    string Severity,
    string RiskRating,
    string RecommendedCorrectiveAction,
    string DefaultResolutionPeriod,
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
    public static DefectResponse FromEntity(DefectEntity defect)
    {
        return new DefectResponse(
            defect.Id,
            defect.DefectCode,
            defect.DefectName,
            defect.DefectCategory,
            defect.Description,
            defect.Severity,
            defect.RiskRating,
            defect.RecommendedCorrectiveAction,
            defect.DefaultResolutionPeriod,
            defect.IsActive,
            defect.Status,
            defect.CreatedBy,
            defect.CreatedAt,
            defect.UpdatedBy,
            defect.UpdatedAt,
            defect.ClientId,
            defect.TenantId,
            defect.IsDeleted);
    }
}
