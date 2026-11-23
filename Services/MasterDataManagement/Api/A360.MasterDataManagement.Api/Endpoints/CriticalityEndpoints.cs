using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;
using CriticalityEntity = A360.MasterDataManagement.Domain.Entities.Criticality;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class CriticalityEndpoints
{
    public static RouteGroupBuilder MapCriticalityEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/criticalities")
            .WithTags("Criticalities")
            .RequireCriticalityConfigure();

        group.MapGet("", GetCriticalitiesAsync)
            .WithName("GetCriticalities");

        // Literal routes before /{id}
        group.MapGet("/ranges/check", CheckRangesAsync)
            .WithName("CheckCriticalityRanges");

        group.MapPut("/ranges", SaveRangesAsync)
            .WithName("SaveCriticalityRanges");

        group.MapGet("/{id}", GetCriticalityByIdAsync)
            .WithName("GetCriticalityById");

        group.MapPost("", CreateCriticalityAsync)
            .WithName("CreateCriticality");

        group.MapPut("/{id}", UpdateCriticalityAsync)
            .WithName("UpdateCriticality");

        group.MapPatch("/{id}/status", UpdateCriticalityStatusAsync)
            .WithName("UpdateCriticalityStatus");

        group.MapDelete("/{id}", DeleteCriticalityAsync)
            .WithName("DeleteCriticality");

        return group;
    }

    private static async Task<IResult> GetCriticalitiesAsync(
        string? search,
        string? status,
        string? riskLevel,
        string? maintenancePriority,
        bool? defaultCriticality,
        string? clientId,
        ICriticalityRepository repository,
        CancellationToken cancellationToken)
    {
        var criticalities = await repository.SearchAsync(
            new CriticalityFilter(search, status, riskLevel, maintenancePriority, defaultCriticality, clientId),
            cancellationToken);

        return Results.Ok(
            criticalities.Select(CriticalityResponse.FromEntity));
    }

    private static async Task<IResult> GetCriticalityByIdAsync(
        string id,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> CreateCriticalityAsync(
        CreateCriticalityRequest request,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var criticality = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            criticality.Code,
            criticality.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A criticality with code '{criticality.Code}' already exists." });
        }

        var rangeErrors = await RangeErrorsAsync(repository, criticality, cancellationToken);

        if (rangeErrors.Count > 0)
        {
            return Results.ValidationProblem(rangeErrors);
        }

        criticality = await repository.CreateAsync(
            criticality,
            cancellationToken);

        if (criticality.DefaultCriticality)
        {
            await repository.ClearDefaultAsync(
                criticality.ClientId,
                criticality.Id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/criticalities/{criticality.Id}",
            CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> UpdateCriticalityAsync(
        string id,
        UpdateCriticalityRequest request,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(criticality);

        var existing = await repository.GetByCodeAsync(
            criticality.Code,
            criticality.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A criticality with code '{criticality.Code}' already exists." });
        }

        var rangeErrors = await RangeErrorsAsync(repository, criticality, cancellationToken);

        if (rangeErrors.Count > 0)
        {
            return Results.ValidationProblem(rangeErrors);
        }

        var updated = await repository.UpdateAsync(
            id,
            criticality,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (criticality.DefaultCriticality)
        {
            await repository.ClearDefaultAsync(
                criticality.ClientId,
                id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> UpdateCriticalityStatusAsync(
        string id,
        UpdateCriticalityStatusRequest request,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
        {
            return Results.NotFound();
        }

        criticality.Status = request.Status;
        criticality.UpdatedBy = request.UpdatedBy;
        criticality.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            criticality,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(CriticalityResponse.FromEntity(criticality));
    }

    private static async Task<IResult> DeleteCriticalityAsync(
        string id,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid criticality id." });
        }

        var criticality = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (criticality is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Criticality",
            criticality.Id,
            criticality.CriticalityName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    // A level may not overlap the other active levels of its client. Gaps are only checked when the whole set is saved
    // (ranges/check and PUT ranges), since a set is built one level at a time.
    private static async Task<Dictionary<string, string[]>> RangeErrorsAsync(
        ICriticalityRepository repository,
        CriticalityEntity level,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if ((level.MinScore is null) != (level.MaxScore is null))
        {
            errors["MinScore"] = ["Enter both the minimum and the maximum score, or neither."];
            return errors;
        }

        if (level.MinScore is null || !CriticalityRanges.IsActive(level))
        {
            return errors;
        }

        var others = (await repository.SearchAsync(new CriticalityFilter(null, null, null, null, null, level.ClientId), cancellationToken))
            .Where(other => other.Id != level.Id);
        var problems = CriticalityRanges.Check(others.Append(level), requireFullCoverage: false);

        if (problems.Count > 0)
        {
            errors["MinScore"] = [.. problems];
        }

        return errors;
    }

    // Do the active level ranges of the client cover 20-100 without overlap or gaps?
    private static async Task<IResult> CheckRangesAsync(
        string? clientId,
        ICriticalityRepository repository,
        CancellationToken cancellationToken)
    {
        var levels = await repository.SearchAsync(new CriticalityFilter(null, null, null, null, null, clientId), cancellationToken);
        var problems = CriticalityRanges.Check(levels, requireFullCoverage: true);

        return Results.Ok(new { valid = problems.Count == 0, problems });
    }

    // Saves the ranges, colours and order of several levels at once; the whole set must cover 20-100 without overlap or gaps.
    private static async Task<IResult> SaveRangesAsync(
        SaveCriticalityRangesRequest request,
        ICriticalityRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (request.Levels is null || request.Levels.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Levels"] = ["Levels are required"] });
        }

        var all = (await repository.SearchAsync(new CriticalityFilter(null, null, null, null, null, request.ClientId), cancellationToken)).ToList();
        var byId = all.ToDictionary(level => level.Id);
        var colorPattern = new System.Text.RegularExpressions.Regex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
        var errors = new Dictionary<string, string[]>();

        foreach (var item in request.Levels)
        {
            if (item.Id is null || !byId.TryGetValue(item.Id, out var level))
            {
                errors["Levels"] = [$"Level '{item.Id}' was not found."];
                continue;
            }

            if (item.MinScore is null || item.MaxScore is null)
            {
                errors[level.CriticalityName] = ["Enter the minimum and the maximum score."];
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.Color) && !colorPattern.IsMatch(item.Color.Trim()))
            {
                errors[level.CriticalityName] = ["Color must be a hex colour such as #C62828"];
                continue;
            }

            level.MinScore = item.MinScore;
            level.MaxScore = item.MaxScore;
            level.DisplayOrder = item.DisplayOrder ?? level.DisplayOrder;
            level.Color = string.IsNullOrWhiteSpace(item.Color) ? level.Color : item.Color.Trim().ToUpperInvariant();
            level.UpdatedBy = request.UpdatedBy;
            level.UpdatedAt = DateTime.UtcNow;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var problems = CriticalityRanges.Check(all, requireFullCoverage: true);

        if (problems.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Ranges"] = [.. problems] });
        }

        foreach (var item in request.Levels)
        {
            var level = byId[item.Id!];
            await repository.UpdateAsync(level.Id, level, cancellationToken);
            await eventLogger.LogAsync("Criticality", level.Id, level.CriticalityName, EventAction.Updated, cancellationToken);
        }

        return Results.Ok(all.Where(level => request.Levels.Any(item => item.Id == level.Id)).Select(CriticalityResponse.FromEntity));
    }
}

/// <summary>Only users with Assets Approve (AssetCriticality configure) may change the criticality levels; reading needs no extra check.</summary>
public static class CriticalityConfigureFilter
{
    public static RouteGroupBuilder RequireCriticalityConfigure(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;

            if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))
            {
                return await next(context);
            }

            var granted = await http.RequestServices.GetRequiredService<ICriticalityPermissionService>().GetAsync(http.RequestAborted);

            if (granted is null)
            {
                return Results.Json(new { message = "Your permissions could not be checked. Try again in a moment." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return granted.Contains(AssetCriticalityPermissions.Configure)
                ? await next(context)
                : Results.Json(new { message = "Only users who can approve in Assets may change the criticality levels." }, statusCode: StatusCodes.Status403Forbidden);
        });

        return group;
    }
}
