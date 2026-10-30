namespace A360.Repository.Repositories;

public interface IMongoIndexConfigurator
{
    Task CreateIndexesAsync(CancellationToken cancellationToken = default);
}
