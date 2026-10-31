namespace A360.Repository.Sequences;

public interface ISequenceGenerator
{
    Task<long> GetNextValueAsync(string sequenceName, CancellationToken cancellationToken = default);
}
