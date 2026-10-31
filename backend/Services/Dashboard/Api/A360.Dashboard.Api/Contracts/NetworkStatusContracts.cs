using A360.Dashboard.Repository.Repositories;

namespace A360.Dashboard.Api.Contracts;

public sealed record NetworkStatusCountResponse(
    long TotalOnline,
    long TotalOffline)
{
    public static NetworkStatusCountResponse FromCounts(NetworkStatusCounts counts)
    {
        return new NetworkStatusCountResponse(counts.Online, counts.Offline);
    }
}

public sealed record TagRecordCountResponse(
    string TagId,
    long Count,
    string? AssetId,
    string? AssetName)
{
    public static TagRecordCountResponse FromTagRecordCount(TagRecordCount tag)
    {
        return new TagRecordCountResponse(tag.TagId, tag.Count, tag.AssetId, tag.AssetName);
    }
}
