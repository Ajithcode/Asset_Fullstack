using SkillEntity = A360.MasterDataManagement.Domain.Entities.Skill;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ISkillRepository : IMongoRepository<SkillEntity>
{
    Task<IReadOnlyCollection<SkillEntity>> SearchAsync(SkillFilter filter, CancellationToken cancellationToken = default);

    Task<SkillEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record SkillFilter(
    string? Search,
    string? Status,
    string? Category,
    string? Type,
    string? ClientId);
