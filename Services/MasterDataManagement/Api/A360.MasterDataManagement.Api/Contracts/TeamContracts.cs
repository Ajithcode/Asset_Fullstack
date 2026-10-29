using TeamEntity = A360.MasterDataManagement.Domain.Entities.Team;

namespace A360.MasterDataManagement.Api.Contracts;

// Members are technicians picked by id; each one's assigned team is set to this team.
// On update, leaving Members out keeps the current members, while an empty list removes them all.
public sealed record CreateTeamRequest(
    string? TeamName,
    string? Code,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? TeamLeader,
    string? Description,
    List<MasterDataReferenceDto>? Members,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public TeamEntity ToEntity()
    {
        return new TeamEntity
        {
            TeamName = TeamName!.Trim(),
            Code = Code!.Trim(),
            Department = Department?.ToReference(),
            TeamLeader = TeamLeader?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateTeamRequest(
    string? TeamName,
    string? Code,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? TeamLeader,
    string? Description,
    List<MasterDataReferenceDto>? Members,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(TeamEntity team)
    {
        team.TeamName = TeamName!.Trim();
        team.Code = Code!.Trim();
        team.Department = Department?.ToReference();
        team.TeamLeader = TeamLeader?.ToReference();
        team.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        team.UpdatedBy = UpdatedBy;
        team.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            team.Status = Status;
        }
    }
}

public sealed record UpdateTeamStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record TeamResponse(
    string Id,
    string TeamName,
    string Code,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? TeamLeader,
    string? Description,
    IReadOnlyList<MasterDataReferenceDto> Members,
    int MemberCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static TeamResponse FromEntity(
        TeamEntity team,
        IReadOnlyList<MasterDataReferenceDto> members)
    {
        return new TeamResponse(
            team.Id ?? string.Empty,
            team.TeamName ?? string.Empty,
            team.Code ?? string.Empty,
            team.Department is null
                ? null
                : MasterDataReferenceDto.FromReference(team.Department),
            team.TeamLeader is null
                ? null
                : MasterDataReferenceDto.FromReference(team.TeamLeader),
            team.Description,
            members,
            members.Count,
            team.CreatedBy,
            team.CreatedAt,
            team.UpdatedBy,
            team.UpdatedAt,
            team.ClientId,
            team.TenantId,
            team.Status,
            team.IsDeleted
        );
    }
}
