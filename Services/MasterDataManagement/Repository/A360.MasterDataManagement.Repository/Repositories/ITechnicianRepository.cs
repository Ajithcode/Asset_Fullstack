using TechnicianEntity = A360.MasterDataManagement.Domain.Entities.Technician;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ITechnicianRepository : IMongoRepository<TechnicianEntity>
{
    Task<IReadOnlyCollection<TechnicianEntity>> SearchAsync(TechnicianFilter filter, CancellationToken cancellationToken = default);

    Task<TechnicianEntity?> GetByTechnicianIdAsync(string technicianId, string? clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TechnicianEntity>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TechnicianEntity>> GetByTeamsAsync(IEnumerable<string> teamIds, CancellationToken cancellationToken = default);

    // Technicians whose record id, technician id or name matches the value (case-insensitive).
    Task<IReadOnlyCollection<TechnicianEntity>> FindByReferenceAsync(string value, CancellationToken cancellationToken = default);

    Task<bool> AnyWithTeamAsync(string teamId, CancellationToken cancellationToken = default);

    Task UpdateTeamNameAsync(string teamId, string teamName, CancellationToken cancellationToken = default);

    // Makes the team's members exactly the given technicians: listed ones are moved into it, others it had are cleared.
    Task SetTeamMembersAsync(string teamId, string teamName, IReadOnlyCollection<string> technicianIds, CancellationToken cancellationToken = default);
}

public sealed record TechnicianFilter(
    string? Search,
    string? Status,
    string? PrimarySkill,
    string? AssignedTeamId,
    string? ClientId);
