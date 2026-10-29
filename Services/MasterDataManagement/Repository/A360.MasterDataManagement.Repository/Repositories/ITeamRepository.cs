using TeamEntity = A360.MasterDataManagement.Domain.Entities.Team;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ITeamRepository : IMongoRepository<TeamEntity>
{
    Task<IReadOnlyCollection<TeamEntity>> SearchAsync(TeamFilter filter, CancellationToken cancellationToken = default);

    Task<TeamEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithDepartmentAsync(string departmentId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithTeamLeaderAsync(string technicianId, CancellationToken cancellationToken = default);

    Task UpdateDepartmentNameAsync(string departmentId, string departmentName, CancellationToken cancellationToken = default);

    Task UpdateTeamLeaderNameAsync(string technicianId, string technicianName, CancellationToken cancellationToken = default);
}

public sealed record TeamFilter(
    string? Search,
    string? Status,
    string? DepartmentId,
    string? TeamLeaderId,
    // Limits the search to these teams (e.g. the teams of matching technicians) when set.
    IReadOnlyCollection<string>? TeamIds,
    string? ClientId);
