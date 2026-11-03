using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Risk mitigation actions of registered assets, for the Risk Mitigation tab under Criticality.</summary>
/// <remarks>View History reads GET /api/eventlogs?entityType=AssetRiskMitigationAction&amp;entityId={id}.</remarks>
public static class AssetRiskMitigationActionEndpoints
{
    private const string EntityType = "AssetRiskMitigationAction";

    public static RouteGroupBuilder MapAssetRiskMitigationActionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-risk-mitigation-actions").WithTags("Asset Risk Mitigation Actions");

        group.MapGet("", GetActionsAsync).WithName("GetAssetRiskMitigationActions");
        group.MapGet("/statuses", () => Results.Ok(AssetRiskMitigationStatuses.All)).WithName("GetAssetRiskMitigationStatuses");
        group.MapGet("/{id}", GetActionByIdAsync).WithName("GetAssetRiskMitigationActionById");
        group.MapPost("", CreateActionAsync).WithName("CreateAssetRiskMitigationAction");
        group.MapPatch("/{id}/status", UpdateActionStatusAsync).WithName("UpdateAssetRiskMitigationActionStatus");

        return group;
    }

    private static async Task<IResult> GetActionsAsync(
        IAssetRiskMitigationActionRepository repository,
        CancellationToken cancellationToken,
        string? assetRegistrationId = null,
        string? status = null,
        int page = 1,
        int pageSize = AssetRiskMitigationActionRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetRiskMitigationActionRepository.MaxPageSize ? AssetRiskMitigationActionRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetRiskMitigationActionQuery(assetRegistrationId, AssetRiskMitigationStatuses.Normalize(status) ?? status, page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetRiskMitigationActionListResponse(
            result.Items.Select(AssetRiskMitigationActionResponse.FromEntity).ToList(),
            result.TotalCount,
            result.OpenCount,
            page,
            pageSize));
    }

    private static async Task<IResult> GetActionByIdAsync(
        string id,
        IAssetRiskMitigationActionRepository repository,
        CancellationToken cancellationToken)
    {
        var action = await repository.GetByIdAsync(id, cancellationToken);

        return action is null || action.IsDeleted
            ? ActionNotFound(id)
            : Results.Ok(AssetRiskMitigationActionResponse.FromEntity(action));
    }

    private static async Task<IResult> CreateActionAsync(
        CreateAssetRiskMitigationActionRequest request,
        IAssetRiskMitigationActionRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var assetRegistrationId = request.AssetRegistrationId!.Trim();
        var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        if (asset is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AssetRegistrationId"] = [$"Asset '{assetRegistrationId}' was not found"]
            });
        }

        var action = request.ToEntity(asset, currentUserAccessor.GetCurrentUser());

        await repository.CreateAsync(action, cancellationToken);
        await eventLogger.LogAsync(EntityType, action.Id, Describe(action), EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-risk-mitigation-actions/{action.Id}", AssetRiskMitigationActionResponse.FromEntity(action));
    }

    /// <summary>The Status dropdown in the list: Open, In Progress, Completed or Cancelled.</summary>
    private static async Task<IResult> UpdateActionStatusAsync(
        string id,
        UpdateAssetRiskMitigationActionStatusRequest request,
        IAssetRiskMitigationActionRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var action = await repository.GetByIdAsync(id, cancellationToken);
        if (action is null || action.IsDeleted)
        {
            return ActionNotFound(id);
        }

        request.ApplyTo(action, currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(id, action, cancellationToken))
        {
            return ActionNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, action.Id, Describe(action), EventAction.Updated, cancellationToken);

        return Results.Ok(AssetRiskMitigationActionResponse.FromEntity(action));
    }

    private static string Describe(AssetRiskMitigationAction action) =>
        $"{action.AssetRegistrationId} - {action.Action} ({action.Status})";

    private static IResult ActionNotFound(string id)
    {
        return Results.NotFound(new { message = $"Risk mitigation action '{id}' was not found." });
    }
}
