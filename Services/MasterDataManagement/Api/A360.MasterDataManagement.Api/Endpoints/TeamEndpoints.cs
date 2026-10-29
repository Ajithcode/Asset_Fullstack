using TeamEntity = A360.MasterDataManagement.Domain.Entities.Team;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class TeamEndpoints
{
    public static RouteGroupBuilder MapTeamEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/teams")
            .WithTags("Teams");

        group.MapGet("", GetTeamsAsync)
            .WithName("GetTeams");

        group.MapGet("/{id}", GetTeamByIdAsync)
            .WithName("GetTeamById");

        group.MapPost("", CreateTeamAsync)
            .WithName("CreateTeam");

        group.MapPut("/{id}", UpdateTeamAsync)
            .WithName("UpdateTeam");

        group.MapPatch("/{id}/status", UpdateTeamStatusAsync)
            .WithName("UpdateTeamStatus");

        group.MapDelete("/{id}", DeleteTeamAsync)
            .WithName("DeleteTeam");

        return group;
    }

    private static async Task<IResult> GetTeamsAsync(
        string? search,
        string? status,
        string? departmentId,
        string? teamLeaderId,
        string? member,
        string? clientId,
        ITeamRepository repository,
        ITechnicianRepository technicians,
        CancellationToken cancellationToken)
    {
        // Technicians know their team, so the member filter becomes a list of team ids.
        IReadOnlyCollection<string>? teamIds = null;

        if (!string.IsNullOrWhiteSpace(member))
        {
            teamIds = (await technicians.FindByReferenceAsync(member, cancellationToken))
                .Where(x => x.AssignedTeam?.Id is not null)
                .Select(x => x.AssignedTeam!.Id!)
                .Distinct()
                .ToList();
        }

        var teams = await repository.SearchAsync(
            new TeamFilter(search, status, departmentId, teamLeaderId, teamIds, clientId),
            cancellationToken);

        // One query for the members of every team in the page.
        var members = (await technicians.GetByTeamsAsync(
                teams.Select(x => x.Id),
                cancellationToken))
            .ToLookup(x => x.AssignedTeam!.Id);

        return Results.Ok(
            teams.Select(x => TeamResponse.FromEntity(x, ToMemberList(members[x.Id]))));
    }

    private static async Task<IResult> GetTeamByIdAsync(
        string id,
        ITeamRepository repository,
        ITechnicianRepository technicians,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid team id." });
        }

        var team = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (team is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Team",
            team.Id,
            team.TeamName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(team, technicians, cancellationToken));
    }

    private static async Task<IResult> CreateTeamAsync(
        CreateTeamRequest request,
        ITeamRepository repository,
        IDepartmentRepository departments,
        ITechnicianRepository technicians,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var team = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            team.Code,
            team.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A team with code '{team.Code}' already exists." });
        }

        var memberIds = request.Members?.Select(x => x.Id!.Trim()).ToList();

        var referenceError = await ResolveReferencesAsync(
            team,
            memberIds,
            departments,
            technicians,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        team = await repository.CreateAsync(
            team,
            cancellationToken);

        if (memberIds is not null)
        {
            await technicians.SetTeamMembersAsync(
                team.Id,
                team.TeamName,
                memberIds,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Team",
            team.Id,
            team.TeamName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/teams/{team.Id}",
            await ToResponseAsync(team, technicians, cancellationToken));
    }

    private static async Task<IResult> UpdateTeamAsync(
        string id,
        UpdateTeamRequest request,
        ITeamRepository repository,
        IDepartmentRepository departments,
        ITechnicianRepository technicians,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid team id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var team = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (team is null)
        {
            return Results.NotFound();
        }

        var previousName = team.TeamName;

        request.ApplyTo(team);

        var existing = await repository.GetByCodeAsync(
            team.Code,
            team.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A team with code '{team.Code}' already exists." });
        }

        var memberIds = request.Members?.Select(x => x.Id!.Trim()).ToList();

        var referenceError = await ResolveReferencesAsync(
            team,
            memberIds,
            departments,
            technicians,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            team,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (memberIds is not null)
        {
            await technicians.SetTeamMembersAsync(
                id,
                team.TeamName,
                memberIds,
                cancellationToken);
        }

        if (previousName != team.TeamName)
        {
            await technicians.UpdateTeamNameAsync(
                id,
                team.TeamName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Team",
            team.Id,
            team.TeamName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(team, technicians, cancellationToken));
    }

    private static async Task<IResult> UpdateTeamStatusAsync(
        string id,
        UpdateTeamStatusRequest request,
        ITeamRepository repository,
        ITechnicianRepository technicians,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid team id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var team = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (team is null)
        {
            return Results.NotFound();
        }

        team.Status = request.Status;
        team.UpdatedBy = request.UpdatedBy;
        team.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            team,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Team",
            team.Id,
            team.TeamName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(team, technicians, cancellationToken));
    }

    private static async Task<IResult> DeleteTeamAsync(
        string id,
        ITeamRepository repository,
        ITechnicianRepository technicians,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid team id." });
        }

        var team = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (team is null)
        {
            return Results.NotFound();
        }

        if (await technicians.AnyWithTeamAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Technicians are still assigned to this team. Move them to another team first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Team",
            team.Id,
            team.TeamName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the department, team leader and every listed member exist, copying the department and leader
    /// names onto the team. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        TeamEntity team,
        IReadOnlyCollection<string>? memberIds,
        IDepartmentRepository departments,
        ITechnicianRepository technicians,
        CancellationToken cancellationToken)
    {
        if (team.Department is not null)
        {
            var departmentError = await MasterDataReferenceResolver.ResolveDepartmentsAsync(
                [team.Department],
                departments,
                cancellationToken);

            if (departmentError is not null)
            {
                return departmentError;
            }
        }

        var leaderError = await MasterDataReferenceResolver.ResolveTechnicianAsync(
            team.TeamLeader,
            technicians,
            cancellationToken);

        if (leaderError is not null)
        {
            return leaderError;
        }

        if (memberIds is null || memberIds.Count == 0)
        {
            return null;
        }

        var invalid = memberIds.FirstOrDefault(x => !MongoObjectId.IsValid(x));

        if (invalid is null)
        {
            var found = (await technicians.GetByIdsAsync(memberIds, cancellationToken))
                .Select(x => x.Id)
                .ToHashSet();

            invalid = memberIds.FirstOrDefault(x => !found.Contains(x));
        }

        return invalid is null
            ? null
            : Results.BadRequest(
                new { message = $"Technician '{invalid}' was not found." });
    }

    private static async Task<TeamResponse> ToResponseAsync(
        TeamEntity team,
        ITechnicianRepository technicians,
        CancellationToken cancellationToken)
    {
        var members = await technicians.GetByTeamsAsync(
            [team.Id],
            cancellationToken);

        return TeamResponse.FromEntity(team, ToMemberList(members));
    }

    private static IReadOnlyList<MasterDataReferenceDto> ToMemberList(
        IEnumerable<A360.MasterDataManagement.Domain.Entities.Technician> technicians)
    {
        return technicians
            .Select(x => new MasterDataReferenceDto(x.Id, x.TechnicianName))
            .ToList();
    }
}
