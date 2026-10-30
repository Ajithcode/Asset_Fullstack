using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetFieldLayoutRepository : IMongoRepository<AssetFieldLayout>
{
    /// <summary>Layout names are unique, ignoring case.</summary>
    Task<bool> NameExistsAsync(string layoutName, string? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssetFieldLayout>> GetUsingGroupAsync(string groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssetFieldLayout>> GetUsingFieldAsync(string fieldName, CancellationToken cancellationToken = default);

    /// <summary>Replaces a field name in every layout section that lists it, after a field is renamed.</summary>
    Task RenameFieldAsync(string oldFieldName, string newFieldName, CancellationToken cancellationToken = default);
}
