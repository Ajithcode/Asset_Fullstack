
using ResourceMasterEntity = A360.Wip.Domain.Entities.ResourceMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateResourceMasterRequest(
    string? ResourceId,
    string? AssetId,
    string? AssetName,
    string? ResourceName,
    string? ResourceType,
    List<string>? SkillSet,
    string? DepartmentId,
    string? ContactNumber,
    string? Email,
    string? AvailabilityStatus,
    string? ShiftId,
    double? CostPerHour,
    string? CertificationDetails,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public ResourceMasterEntity ToEntity()
    {
        return new ResourceMasterEntity
        {
            ResourceId = ResourceId,
            AssetId = AssetId,
            AssetName = AssetName,

            ResourceName = ResourceName,
            ResourceType = ResourceType,
            SkillSet = SkillSet ?? [],

            DepartmentId = DepartmentId,
            ContactNumber = ContactNumber,
            Email = Email,

            AvailabilityStatus = AvailabilityStatus,
            ShiftId = ShiftId,
            CostPerHour = CostPerHour,
            CertificationDetails = CertificationDetails,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateResourceMasterRequest(
    string? AssetId,
    string? AssetName,
    string? ResourceName,
    string? ResourceType,
    List<string>? SkillSet,
    string? DepartmentId,
    string? ContactNumber,
    string? Email,
    string? AvailabilityStatus,
    string? ShiftId,
    double? CostPerHour,
    string? CertificationDetails,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(ResourceMasterEntity resourceMaster)
    {
        resourceMaster.AssetId = AssetId;
        resourceMaster.AssetName = AssetName;

        resourceMaster.ResourceName = ResourceName;
        resourceMaster.ResourceType = ResourceType;
        resourceMaster.SkillSet = SkillSet ?? [];

        resourceMaster.DepartmentId = DepartmentId;
        resourceMaster.ContactNumber = ContactNumber;
        resourceMaster.Email = Email;

        resourceMaster.AvailabilityStatus = AvailabilityStatus;
        resourceMaster.ShiftId = ShiftId;
        resourceMaster.CostPerHour = CostPerHour;
        resourceMaster.CertificationDetails = CertificationDetails;

        resourceMaster.UpdatedBy = UpdatedBy;
        resourceMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            resourceMaster.Status = Status;
        }
    }
}

public sealed record ResourceMasterResponse(
    string Id,
    string ResourceId,
    string? AssetId,
    string? AssetName,
    string ResourceName,
    string? ResourceType,
    List<string> SkillSet,
    string? DepartmentId,
    string? ContactNumber,
    string? Email,
    string? AvailabilityStatus,
    string? ShiftId,
    double? CostPerHour,
    string? CertificationDetails,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ResourceMasterResponse FromEntity(ResourceMasterEntity resourceMaster)
    {
        return new ResourceMasterResponse(
            resourceMaster.Id ?? string.Empty,
            resourceMaster.ResourceId ?? string.Empty,
            resourceMaster.AssetId,
            resourceMaster.AssetName,

            resourceMaster.ResourceName ?? string.Empty,
            resourceMaster.ResourceType,
            resourceMaster.SkillSet,

            resourceMaster.DepartmentId,
            resourceMaster.ContactNumber,
            resourceMaster.Email,

            resourceMaster.AvailabilityStatus,
            resourceMaster.ShiftId,
            resourceMaster.CostPerHour,
            resourceMaster.CertificationDetails,

            resourceMaster.CreatedBy,
            resourceMaster.CreatedAt,
            resourceMaster.UpdatedBy,
            resourceMaster.UpdatedAt,

            resourceMaster.ClientId,
            resourceMaster.TenantId,
            resourceMaster.Status,
            resourceMaster.IsDeleted
        );
    }
}
