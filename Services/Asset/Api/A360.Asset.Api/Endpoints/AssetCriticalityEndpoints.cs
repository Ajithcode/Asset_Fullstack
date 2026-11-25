using A360.Asset.Api.Contracts;
using A360.Asset.Api.Security;
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
    public static RouteGroupBuilder MapAssetCriticalityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-criticality").WithTags("Asset Criticality").RequireCriticalityView();

        // Landing page: /assets, /kpis, /distribution, /assessments, /filter-options, /export.
        group.MapAssetCriticalityLandingEndpoints();

        // Configuration masters: /config/criteria, /templates, /approval-rules, /reassessment-rules.
        group.MapAssetCriticalityConfigEndpoints();

        group.MapGet("", GetCriticalityListAsync).WithName("GetAssetCriticalityList");
        group.MapGet("/options", GetOptions).WithName("GetAssetCriticalityOptions");
        group.MapGet("/current", GetCurrentAssessmentAsync).WithName("GetCurrentAssetCriticality");
        group.MapGet("/history", GetHistoryAsync).WithName("GetAssetCriticalityHistory");
        group.MapGet("/{id}", GetAssessmentByIdAsync).WithName("GetAssetCriticalityById");

        // Phase 3: score-preview, attachments, /assessments (draft, edit, submit, approve, reject), /assets/{id}/versions.
        // The old direct-publish POST / and PUT /{id} are gone: everything goes Draft -> Submit -> Approve.
        group.MapAssetCriticalityAssessmentEndpoints();

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

    private static IResult AssessmentNotFound(string id)
    {
        return Results.NotFound(new { message = $"Criticality assessment '{id}' was not found." });
    }
}
