using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class MasterDataItem : BaseEntity
{
    [BsonElement("master_type")]
    public string MasterType { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("sort_order")]
    public int SortOrder { get; set; }
}
