using AmcVendorEntity = A360.MasterDataManagement.Domain.Entities.AmcVendor;

namespace A360.MasterDataManagement.Api.Contracts;

// Dates are sent and returned as "yyyy-MM-dd".
public sealed record CreateAmcVendorRequest(
    string? AmcVendorName,
    string? Code,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? PrimaryContact,
    MasterDataReferenceDto? AssetType,
    string? Email,
    string? ContractType,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AmcVendorEntity ToEntity()
    {
        return new AmcVendorEntity
        {
            AmcVendorName = AmcVendorName!.Trim(),
            Code = Code!.Trim(),
            StartDate = StartDate!.Value,
            EndDate = EndDate!.Value,
            PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim(),
            AssetType = AssetType?.ToReference(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            ContractType = string.IsNullOrWhiteSpace(ContractType) ? null : ContractType.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAmcVendorRequest(
    string? AmcVendorName,
    string? Code,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? PrimaryContact,
    MasterDataReferenceDto? AssetType,
    string? Email,
    string? ContractType,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AmcVendorEntity amcVendor)
    {
        amcVendor.AmcVendorName = AmcVendorName!.Trim();
        amcVendor.Code = Code!.Trim();
        amcVendor.StartDate = StartDate!.Value;
        amcVendor.EndDate = EndDate!.Value;
        amcVendor.PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim();
        amcVendor.AssetType = AssetType?.ToReference();
        amcVendor.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        amcVendor.ContractType = string.IsNullOrWhiteSpace(ContractType) ? null : ContractType.Trim();

        amcVendor.UpdatedBy = UpdatedBy;
        amcVendor.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            amcVendor.Status = Status;
        }
    }
}

public sealed record UpdateAmcVendorStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AmcVendorResponse(
    string Id,
    string AmcVendorName,
    string Code,
    DateOnly StartDate,
    DateOnly EndDate,
    string? PrimaryContact,
    MasterDataReferenceDto? AssetType,
    string? Email,
    string? ContractType,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AmcVendorResponse FromEntity(AmcVendorEntity amcVendor)
    {
        return new AmcVendorResponse(
            amcVendor.Id ?? string.Empty,
            amcVendor.AmcVendorName ?? string.Empty,
            amcVendor.Code ?? string.Empty,
            amcVendor.StartDate,
            amcVendor.EndDate,
            amcVendor.PrimaryContact,
            amcVendor.AssetType is null
                ? null
                : MasterDataReferenceDto.FromReference(amcVendor.AssetType),
            amcVendor.Email,
            amcVendor.ContractType,
            amcVendor.CreatedBy,
            amcVendor.CreatedAt,
            amcVendor.UpdatedBy,
            amcVendor.UpdatedAt,
            amcVendor.ClientId,
            amcVendor.TenantId,
            amcVendor.Status,
            amcVendor.IsDeleted
        );
    }
}
