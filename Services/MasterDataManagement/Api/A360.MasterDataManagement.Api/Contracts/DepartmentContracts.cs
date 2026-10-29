using DepartmentEntity = A360.MasterDataManagement.Domain.Entities.Department;

namespace A360.MasterDataManagement.Api.Contracts;

// Employees are employee records picked by id; each one's department is set to this department.
// On update, leaving Employees out keeps the current employees, while an empty list removes them all.
public sealed record CreateDepartmentRequest(
    string? DepartmentName,
    string? Code,
    MasterDataReferenceDto? BusinessUnit,
    MasterDataReferenceDto? DepartmentHead,
    List<MasterDataReferenceDto>? Employees,
    List<MasterDataReferenceDto>? Assets,
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
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(DepartmentEntity department)
    {
        department.DepartmentName = DepartmentName!.Trim();
        department.Code = Code!.Trim();
        department.BusinessUnit = BusinessUnit?.ToReference();
        department.DepartmentHead = DepartmentHead?.ToReference();
        department.Assets = Assets?.Select(x => x.ToReference()).ToList() ?? [];

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
