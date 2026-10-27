using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Criticality assessments of registered assets, for the Criticality tab and the Asset Criticality List.</summary>
/// <remarks>View History reads GET /api/eventlogs?entityType=AssetCriticalityAssessment&amp;entityId={id}.</remarks>
public static class AssetCriticalityEndpoints
{
    private const string EntityType = "AssetCriticalityAssessment";

    public static RouteGroupBuilder MapAssetCriticalityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-criticality").WithTags("Asset Criticality");

        group.MapGet("", GetCriticalityListAsync).WithName("GetAssetCriticalityList");
        group.MapGet("/options", GetOptions).WithName("GetAssetCriticalityOptions");
        group.MapGet("/current", GetCurrentAssessmentAsync).WithName("GetCurrentAssetCriticality");
        group.MapGet("/history", GetHistoryAsync).WithName("GetAssetCriticalityHistory");
        group.MapGet("/{id}", GetAssessmentByIdAsync).WithName("GetAssetCriticalityById");
        group.MapPost("", CreateAssessmentAsync).WithName("CreateAssetCriticality");

        return group;
    }

    /// <summary>Asset Criticality List: the current assessment of each asset.</summary>
    private static async Task<IResult> GetCriticalityListAsync(
        IAssetCriticalityAssessmentRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? category = null,
        string? location = null,
        string? criticalityLevel = null,
        int page = 1,
        int pageSize = AssetCriticalityAssessmentRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetCriticalityAssessmentRepository.MaxPageSize ? AssetCriticalityAssessmentRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryCurrentAsync(
            new AssetCriticalityQuery(search, category, location, criticalityLevel, page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetCriticalityListResponse(
            result.Items.Select(AssetCriticalityResponse.FromEntity).ToList(),
            result.TotalCount,
            page,
            pageSize));
    }

    private static IResult GetOptions()
    {
        return Results.Ok(new AssetCriticalityOptionsResponse(
            AssetRiskRatings.All,
            AssetRiskRatings.All,
            AssetCriticalityLevels.All,
            AssetCriticalityCriteria.Defaults));
    }

    /// <summary>Criticality Overview, Score Breakdown, Matrix and Assessment Details of one asset.</summary>
    private static async Task<IResult> GetCurrentAssessmentAsync(
        string assetRegistrationId,
        IAssetCriticalityAssessmentRepository repository,
        CancellationToken cancellationToken)
    {
        var assessment = await repository.GetCurrentAsync(assetRegistrationId.Trim(), cancellationToken);

        return assessment is null
            ? Results.NotFound(new { message = $"Asset '{assetRegistrationId}' has no criticality assessment yet." })
            : Results.Ok(AssetCriticalityResponse.FromEntity(assessment));
    }

    /// <summary>Review History: every assessment of one asset, latest first.</summary>
    private static async Task<IResult> GetHistoryAsync(
        string assetRegistrationId,
        IAssetCriticalityAssessmentRepository repository,
        CancellationToken cancellationToken)
    {
        var history = await repository.GetHistoryAsync(assetRegistrationId.Trim(), cancellationToken);
        return Results.Ok(history.Select(AssetCriticalityResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetAssessmentByIdAsync(
        string id,
        IAssetCriticalityAssessmentRepository repository,
        CancellationToken cancellationToken)
    {
        var assessment = await repository.GetByIdAsync(id, cancellationToken);

        return assessment is null || assessment.IsDeleted
            ? AssessmentNotFound(id)
            : Results.Ok(AssetCriticalityResponse.FromEntity(assessment));
    }

    /// <summary>Assess Asset / Re-assess: saves a new assessment and makes it the asset's current one.</summary>
    private static async Task<IResult> CreateAssessmentAsync(
        CreateAssetCriticalityRequest request,
        IAssetCriticalityAssessmentRepository repository,
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

        var previous = await repository.GetCurrentAsync(assetRegistrationId, cancellationToken);
        var assessment = request.ToEntity(asset, previous, currentUserAccessor.GetCurrentUser());

        await repository.ClearCurrentAsync(assetRegistrationId, cancellationToken);
        await repository.CreateAsync(assessment, cancellationToken);
        await eventLogger.LogAsync(EntityType, assessment.Id, Describe(assessment), EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-criticality/{assessment.Id}", AssetCriticalityResponse.FromEntity(assessment));
    }

    private static string Describe(AssetCriticalityAssessment assessment) =>
        $"{assessment.AssetRegistrationId} - {assessment.CriticalityLevel} ({assessment.CriticalityScore}/100)";

    private static IResult AssessmentNotFound(string id)
    {
        return Results.NotFound(new { message = $"Criticality assessment '{id}' was not found." });
    }
}
