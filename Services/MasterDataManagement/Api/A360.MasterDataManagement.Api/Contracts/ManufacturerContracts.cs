using ManufacturerEntity = A360.MasterDataManagement.Domain.Entities.Manufacturer;

namespace A360.MasterDataManagement.Api.Contracts;

// Models are asset models picked by id. On update, leaving Models out keeps the current models,
// while an empty list removes them all from this manufacturer.
public sealed record CreateManufacturerRequest(
    string? ManufacturerName,
    string? Code,
    string? Country,
    string? Website,
    string? Description,
    string? ContactPerson,
    string? Email,
    string? PhoneNumber,
    List<MasterDataReferenceDto>? Models,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ManufacturerEntity ToEntity()
    {
        return new ManufacturerEntity
        {
            ManufacturerName = ManufacturerName!.Trim(),
            Code = Code!.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
            Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            ContactPerson = string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateManufacturerRequest(
    string? ManufacturerName,
    string? Code,
    string? Country,
    string? Website,
    string? Description,
    string? ContactPerson,
    string? Email,
    string? PhoneNumber,
    List<MasterDataReferenceDto>? Models,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ManufacturerEntity manufacturer)
    {
        manufacturer.ManufacturerName = ManufacturerName!.Trim();
        manufacturer.Code = Code!.Trim();
        manufacturer.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
        manufacturer.Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim();
        manufacturer.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        manufacturer.ContactPerson = string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson.Trim();
        manufacturer.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        manufacturer.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();

        manufacturer.UpdatedBy = UpdatedBy;
        manufacturer.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            manufacturer.Status = Status;
        }
    }
}

public sealed record UpdateManufacturerStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ManufacturerResponse(
    string Id,
    string ManufacturerName,
    string Code,
    string? Country,
    string? Website,
    string? Description,
    string? ContactPerson,
    string? Email,
    string? PhoneNumber,
    IReadOnlyList<MasterDataReferenceDto> Models,
    int ModelCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ManufacturerResponse FromEntity(
        ManufacturerEntity manufacturer,
        IReadOnlyList<MasterDataReferenceDto> models)
    {
        return new ManufacturerResponse(
            manufacturer.Id ?? string.Empty,
            manufacturer.ManufacturerName ?? string.Empty,
            manufacturer.Code ?? string.Empty,
            manufacturer.Country,
            manufacturer.Website,
            manufacturer.Description,
            manufacturer.ContactPerson,
            manufacturer.Email,
            manufacturer.PhoneNumber,
            models,
            models.Count,
            manufacturer.CreatedBy,
            manufacturer.CreatedAt,
            manufacturer.UpdatedBy,
            manufacturer.UpdatedAt,
            manufacturer.ClientId,
            manufacturer.TenantId,
            manufacturer.Status,
            manufacturer.IsDeleted
        );
    }
}
