using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A vendor holding an annual maintenance contract (AMC).
public class AmcVendor : BaseEntity
{
    [BsonElement("amc_vendor_name")]
    public string AmcVendorName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Contract period; both days are included.
    [BsonElement("start_date")]
    public DateOnly StartDate { get; set; }

    [BsonElement("end_date")]
    public DateOnly EndDate { get; set; }

    // Name of the main contact person.
    [BsonElement("primary_contact")]
    public string? PrimaryContact { get; set; }

    // Points at an asset type in this service: the kind of asset the contract covers.
    [BsonElement("asset_type")]
    public MasterDataReference? AssetType { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("contract_type")]
    public string? ContractType { get; set; }
}
