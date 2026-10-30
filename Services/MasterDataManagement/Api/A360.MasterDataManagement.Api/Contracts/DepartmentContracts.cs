using DepartmentEntity = A360.MasterDataManagement.Domain.Entities.Department;

namespace A360.MasterDataManagement.Api.Contracts;

// Employees are employee records picked by id; each one's department is set to this department.
// On update, leaving Employees (or Assets) out keeps the current ones, while an empty list removes them all.
public sealed record CreateDepartmentRequest(
    string? DepartmentName,
    string? Code,
    MasterDataReferenceDto? BusinessUnit,
    MasterDataReferenceDto? DepartmentHead,
    List<MasterDataReferenceDto>? Employees,
    List<MasterDataReferenceDto>? Assets,
    MasterDataReferenceDto? ParentDepartment,
    string? Icon,
    string? LogoUrl,
    string? Type,
    string? Deputy,
    string? CostCenter,
    string? Description,
    string? Email,
    string? PhoneCountry,
    string? Phone,
    decimal? AnnualBudget,
    string? Currency,
    string? GlCode,
    decimal? ApprovalLimit,
    bool AllowAssetOwnership,
    bool RequireAssignmentApproval,
    bool IncludeInCostAllocation,
    string? EffectiveFrom,
    string? EffectiveTo,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public DepartmentEntity ToEntity()
    {
        return new DepartmentEntity
        {
            DepartmentName = DepartmentName!.Trim(),
            Code = Code!.Trim(),
            BusinessUnit = BusinessUnit?.ToReference(),
            DepartmentHead = DepartmentHead?.ToReference(),
            Assets = Assets?.Select(x => x.ToReference()).ToList() ?? [],

            ParentDepartment = ParentDepartment?.ToReference(),
            Icon = StructureText.Clean(Icon),
            LogoUrl = StructureText.Clean(LogoUrl),
            Type = StructureText.Clean(Type),
            Deputy = StructureText.Clean(Deputy),
            CostCenter = StructureText.Clean(CostCenter),
            Description = StructureText.Clean(Description),
            Email = StructureText.Clean(Email),
            PhoneCountry = StructureText.Clean(PhoneCountry),
            Phone = StructureText.Clean(Phone),
            AnnualBudget = AnnualBudget,
            Currency = StructureText.Clean(Currency),
            GlCode = StructureText.Clean(GlCode),
            ApprovalLimit = ApprovalLimit,
            AllowAssetOwnership = AllowAssetOwnership,
            RequireAssignmentApproval = RequireAssignmentApproval,
            IncludeInCostAllocation = IncludeInCostAllocation,
            EffectiveFrom = StructureText.Clean(EffectiveFrom),
            EffectiveTo = StructureText.Clean(EffectiveTo),
            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateDepartmentRequest(
    string? DepartmentName,
    string? Code,
    MasterDataReferenceDto? BusinessUnit,
    MasterDataReferenceDto? DepartmentHead,
    List<MasterDataReferenceDto>? Employees,
    List<MasterDataReferenceDto>? Assets,
    MasterDataReferenceDto? ParentDepartment,
    string? Icon,
    string? LogoUrl,
    string? Type,
    string? Deputy,
    string? CostCenter,
    string? Description,
    string? Email,
    string? PhoneCountry,
    string? Phone,
    decimal? AnnualBudget,
    string? Currency,
    string? GlCode,
    decimal? ApprovalLimit,
    bool AllowAssetOwnership,
    bool RequireAssignmentApproval,
    bool IncludeInCostAllocation,
    string? EffectiveFrom,
    string? EffectiveTo,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(DepartmentEntity department)
    {
        department.DepartmentName = DepartmentName!.Trim();
        department.Code = Code!.Trim();
        department.BusinessUnit = BusinessUnit?.ToReference();
        department.DepartmentHead = DepartmentHead?.ToReference();
        if (Assets is not null)
        {
            department.Assets = Assets.Select(x => x.ToReference()).ToList();
        }

        department.ParentDepartment = ParentDepartment?.ToReference();
        department.Icon = StructureText.Clean(Icon);
        department.LogoUrl = StructureText.Clean(LogoUrl);
        department.Type = StructureText.Clean(Type);
        department.Deputy = StructureText.Clean(Deputy);
        department.CostCenter = StructureText.Clean(CostCenter);
        department.Description = StructureText.Clean(Description);
        department.Email = StructureText.Clean(Email);
        department.PhoneCountry = StructureText.Clean(PhoneCountry);
        department.Phone = StructureText.Clean(Phone);
        department.AnnualBudget = AnnualBudget;
        department.Currency = StructureText.Clean(Currency);
        department.GlCode = StructureText.Clean(GlCode);
        department.ApprovalLimit = ApprovalLimit;
        department.AllowAssetOwnership = AllowAssetOwnership;
        department.RequireAssignmentApproval = RequireAssignmentApproval;
        department.IncludeInCostAllocation = IncludeInCostAllocation;
        department.EffectiveFrom = StructureText.Clean(EffectiveFrom);
        department.EffectiveTo = StructureText.Clean(EffectiveTo);
        department.UpdatedBy = UpdatedBy;
        department.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            department.Status = Status;
        }
    }
}

public sealed record UpdateDepartmentStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record DepartmentResponse(
    string Id,
    string DepartmentName,
    string Code,
    MasterDataReferenceDto? BusinessUnit,
    MasterDataReferenceDto? DepartmentHead,
    IReadOnlyList<MasterDataReferenceDto> Employees,
    IReadOnlyList<MasterDataReferenceDto> Assets,
    int EmployeeCount,
    int AssetCount,
    MasterDataReferenceDto? ParentDepartment,
    string? Icon,
    string? LogoUrl,
    string? Type,
    string? Deputy,
    string? CostCenter,
    string? Description,
    string? Email,
    string? PhoneCountry,
    string? Phone,
    decimal? AnnualBudget,
    string? Currency,
    string? GlCode,
    decimal? ApprovalLimit,
    bool AllowAssetOwnership,
    bool RequireAssignmentApproval,
    bool IncludeInCostAllocation,
    string? EffectiveFrom,
    string? EffectiveTo,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static DepartmentResponse FromEntity(
        DepartmentEntity department,
        IReadOnlyList<MasterDataReferenceDto> employees)
    {
        var assets = (department.Assets ?? []).Select(MasterDataReferenceDto.FromReference).ToList();

        return new DepartmentResponse(
            department.Id ?? string.Empty,
            department.DepartmentName ?? string.Empty,
            department.Code ?? string.Empty,
            department.BusinessUnit is null
                ? null
                : MasterDataReferenceDto.FromReference(department.BusinessUnit),
            department.DepartmentHead is null
                ? null
                : MasterDataReferenceDto.FromReference(department.DepartmentHead),
            employees,
            assets,
            employees.Count,
            assets.Count,
            department.ParentDepartment is null ? null : MasterDataReferenceDto.FromReference(department.ParentDepartment),
            department.Icon,
            department.LogoUrl,
            department.Type,
            department.Deputy,
            department.CostCenter,
            department.Description,
            department.Email,
            department.PhoneCountry,
            department.Phone,
            department.AnnualBudget,
            department.Currency,
            department.GlCode,
            department.ApprovalLimit,
            department.AllowAssetOwnership,
            department.RequireAssignmentApproval,
            department.IncludeInCostAllocation,
            department.EffectiveFrom,
            department.EffectiveTo,
            department.CreatedBy,
            department.CreatedAt,
            department.UpdatedBy,
            department.UpdatedAt,
            department.ClientId,
            department.TenantId,
            department.Status,
            department.IsDeleted
        );
    }
}
