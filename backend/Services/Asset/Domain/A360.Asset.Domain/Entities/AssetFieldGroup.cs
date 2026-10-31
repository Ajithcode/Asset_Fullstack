using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A named group of custom fields (e.g. "Technical Specifications").</summary>
/// <remarks>
/// Membership and order are kept on the fields themselves (AssetCustomField.GroupId and DisplayOrder),
/// so a field is in at most one group. Status (Active/Inactive) is kept in BaseEntity.Status.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetFieldGroup : BaseEntity
{
    [BsonElement("group_name")]
    public string GroupName { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }
}
