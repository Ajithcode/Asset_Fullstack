using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Api.Contracts;

// Pieces shared by the vendor masters (suppliers, service providers and AMC vendors).
// Addresses use StructureAddressDto. Dates are sent and returned as "yyyy-MM-dd".

public sealed record VendorDocumentDto(
    string? Name,
    string? Type,
    DateOnly? Expiry)
{
    public VendorDocument ToValue()
    {
        return new VendorDocument
        {
            Name = Name?.Trim() ?? string.Empty,
            Type = StructureText.Clean(Type),
            Expiry = Expiry
        };
    }

    public static List<VendorDocument> ToValues(IEnumerable<VendorDocumentDto>? documents)
    {
        return documents?.Select(x => x.ToValue()).ToList() ?? [];
    }

    public static IReadOnlyList<VendorDocumentDto> FromValues(IEnumerable<VendorDocument>? documents)
    {
        return documents?.Select(x => new VendorDocumentDto(x.Name, x.Type, x.Expiry)).ToList() ?? [];
    }
}
