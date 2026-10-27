using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A fully registered asset, created when a draft is submitted after the last step.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetRegistration : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("basic_information")]
    public AssetBasicInformation BasicInformation { get; set; } = new();

    [BsonElement("classification")]
    public AssetClassification Classification { get; set; } = new();

    [BsonElement("location_assignment")]
    public AssetLocationAssignment LocationAssignment { get; set; } = new();

    [BsonElement("purchase_warranty")]
    public AssetPurchaseWarranty PurchaseWarranty { get; set; } = new();

    [BsonElement("additional_information")]
    public AssetAdditionalInformation AdditionalInformation { get; set; } = new();
}
