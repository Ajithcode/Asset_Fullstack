namespace A360.Dashboard.Repository.Repositories;

public interface INetworkStatusRepository
{
    Task<NetworkStatusCounts> GetStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TagRecordCount>> GetTagRecordCountsAsync(CancellationToken cancellationToken = default);
}

public sealed record NetworkStatusCounts(long Online, long Offline);

public sealed record TagRecordCount(string TagId, long Count, string? AssetId, string? AssetName);
