using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>An asset registration still in progress; each wizard step is saved into its own section.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetRegistrationDraft : BaseEntity
{
    // Generated on the server when step 1 is first saved; kept on the final AssetRegistration after submit.
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    // Furthest step saved so far, so the wizard can resume there.
    [BsonElement("current_step")]
    public int CurrentStep { get; set; }

    [BsonElement("basic_information")]
    public AssetBasicInformation? BasicInformation { get; set; }

    [BsonElement("classification")]
    public AssetClassification? Classification { get; set; }

    [BsonElement("location_assignment")]
    public AssetLocationAssignment? LocationAssignment { get; set; }

    [BsonElement("purchase_warranty")]
    public AssetPurchaseWarranty? PurchaseWarranty { get; set; }

    [BsonElement("additional_information")]
    public AssetAdditionalInformation? AdditionalInformation { get; set; }
}
