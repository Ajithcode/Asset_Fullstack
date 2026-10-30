using TeamEntity = A360.MasterDataManagement.Domain.Entities.Team;

namespace A360.MasterDataManagement.Api.Contracts;

// Members are technicians picked by id; each one's assigned team is set to this team.
// EmployeeMembers are employees picked by id and are stored on the team.
// The team leader is a technician or an employee, picked by id.
// On update, leaving Members or EmployeeMembers out keeps the current ones, while an empty list removes them all.
public sealed record CreateTeamRequest(
    string? TeamName,
    string? Code,
    MasterDataReferenceDto? Department,
    MasterDataReferenceDto? TeamLeader,
    string? Description,
    List<MasterDataReferenceDto>? Members,
    List<MasterDataReferenceDto>? EmployeeMembers,
    MasterDataReferenceDto? Shift,
    List<string>? Responsibilities,
    bool? NotifyLeader,
    bool? AutoAssign,
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
            EmployeeMembers = EmployeeMembers?.Select(x => x.ToReference()).ToList() ?? [],
            Shift = Shift?.ToReference(),
            Responsibilities = StructureText.CleanList(Responsibilities),
            NotifyLeader = NotifyLeader ?? false,
            AutoAssign = AutoAssign ?? false,

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
    List<MasterDataReferenceDto>? EmployeeMembers,
    MasterDataReferenceDto? Shift,
    List<string>? Responsibilities,
    bool? NotifyLeader,
    bool? AutoAssign,
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

        team.Shift = Shift?.ToReference();
        team.Responsibilities = StructureText.CleanList(Responsibilities);
        team.NotifyLeader = NotifyLeader ?? false;
        team.AutoAssign = AutoAssign ?? false;

        if (EmployeeMembers is not null)
        {
            team.EmployeeMembers = EmployeeMembers.Select(x => x.ToReference()).ToList();
        }

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
    IReadOnlyList<MasterDataReferenceDto> EmployeeMembers,
    // Technicians and employees together.
    int MemberCount,
    MasterDataReferenceDto? Shift,
    IReadOnlyList<string> Responsibilities,
    bool NotifyLeader,
    bool AutoAssign,
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
        var employeeMembers = (team.EmployeeMembers ?? [])
            .Select(MasterDataReferenceDto.FromReference)
            .ToList();

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
            employeeMembers,
            members.Count + employeeMembers.Count,
            team.Shift is null ? null : MasterDataReferenceDto.FromReference(team.Shift),
            team.Responsibilities ?? [],
            team.NotifyLeader,
            team.AutoAssign,
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
