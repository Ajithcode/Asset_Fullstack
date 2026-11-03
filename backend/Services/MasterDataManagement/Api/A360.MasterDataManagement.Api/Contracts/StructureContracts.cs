using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Api.Contracts;

// Pieces shared by the organization structure masters (organizations, business units, departments,
// sites, buildings, floors, zones and locations).

public sealed record StructureAddressDto(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? Country,
    string? PostalCode)
{
    public StructureAddress ToValue()
    {
        return new StructureAddress
        {
            Line1 = StructureText.Clean(Line1),
            Line2 = StructureText.Clean(Line2),
            City = StructureText.Clean(City),
            State = StructureText.Clean(State),
            Country = StructureText.Clean(Country),
            PostalCode = StructureText.Clean(PostalCode)
        };
    }

    public static StructureAddressDto? FromValue(StructureAddress? address)
    {
        return address is null
            ? null
            : new StructureAddressDto(address.Line1, address.Line2, address.City, address.State, address.Country, address.PostalCode);
    }
}

public sealed record StructureContactDto(
    string? Name,
    string? Designation,
    string? Email,
    string? PhoneCountry,
    string? Phone)
{
    public StructureContact ToValue()
    {
        return new StructureContact
        {
            Name = StructureText.Clean(Name),
            Designation = StructureText.Clean(Designation),
            Email = StructureText.Clean(Email),
            PhoneCountry = StructureText.Clean(PhoneCountry),
            Phone = StructureText.Clean(Phone)
        };
    }

    public static StructureContactDto? FromValue(StructureContact? contact)
    {
        return contact is null
            ? null
            : new StructureContactDto(contact.Name, contact.Designation, contact.Email, contact.PhoneCountry, contact.Phone);
    }
}

public static class StructureText
{
    /// <summary>Trimmed text, or null when blank.</summary>
    public static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Trimmed, non-blank entries in their original order.</summary>
    public static List<string> CleanList(IEnumerable<string?>? values)
    {
        return values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList() ?? [];
    }
}
