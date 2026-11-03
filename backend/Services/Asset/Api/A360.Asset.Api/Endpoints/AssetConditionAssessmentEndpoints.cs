using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Condition assessments of registered assets, for the Condition Assessment tab of Asset Status &amp; Condition.</summary>
/// <remarks>View History reads GET /api/eventlogs?entityType=AssetConditionAssessment&amp;entityId={id}.</remarks>
public static class AssetConditionAssessmentEndpoints
{
    private const string EntityType = "AssetConditionAssessment";

    public static RouteGroupBuilder MapAssetConditionAssessmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-condition-assessments").WithTags("Asset Condition Assessments");

        group.MapGet("", GetAssessmentsAsync).WithName("GetAssetConditionAssessments");
        group.MapGet("/current", GetCurrentAssessmentAsync).WithName("GetCurrentAssetConditionAssessment");
        group.MapGet("/ratings", () => Results.Ok(AssetConditionRatings.All)).WithName("GetAssetConditionRatings");
        group.MapGet("/{id}", GetAssessmentByIdAsync).WithName("GetAssetConditionAssessmentById");
        group.MapPost("", CreateAssessmentAsync).WithName("CreateAssetConditionAssessment");
        group.MapPut("/{id}", UpdateAssessmentAsync).WithName("UpdateAssetConditionAssessment");

        return group;
    }

    /// <summary>Condition Assessment History, latest first. Also feeds the Condition Trend chart.</summary>
    private static async Task<IResult> GetAssessmentsAsync(
        IAssetConditionAssessmentRepository repository,
        CancellationToken cancellationToken,
        string? assetRegistrationId = null,
        string? overallCondition = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = AssetConditionAssessmentRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetConditionAssessmentRepository.MaxPageSize ? AssetConditionAssessmentRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetConditionAssessmentQuery(assetRegistrationId, overallCondition, ContractText.DateOnly(fromDate), ContractText.DateOnly(toDate), page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetConditionAssessmentListResponse(
            result.Items.Select(AssetConditionAssessmentResponse.FromEntity).ToList(),
            result.TotalCount,
            page,
            pageSize));
    }

    /// <summary>Current Condition Assessment panel: the asset's latest assessment by date.</summary>
    private static async Task<IResult> GetCurrentAssessmentAsync(
        string assetRegistrationId,
        IAssetConditionAssessmentRepository repository,
        CancellationToken cancellationToken)
    {
        var assessment = await repository.GetLatestAsync(assetRegistrationId.Trim(), cancellationToken);

        return assessment is null
            ? Results.NotFound(new { message = $"Asset '{assetRegistrationId}' has no condition assessment yet." })
            : Results.Ok(AssetConditionAssessmentResponse.FromEntity(assessment));
    }

    private static async Task<IResult> GetAssessmentByIdAsync(
        string id,
        IAssetConditionAssessmentRepository repository,
        CancellationToken cancellationToken)
    {
        var assessment = await repository.GetByIdAsync(id, cancellationToken);

        return assessment is null || assessment.IsDeleted
            ? AssessmentNotFound(id)
            : Results.Ok(AssetConditionAssessmentResponse.FromEntity(assessment));
    }

    private static async Task<IResult> CreateAssessmentAsync(
        CreateAssetConditionAssessmentRequest request,
        IAssetConditionAssessmentRepository repository,
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

        var assessment = request.ToEntity(asset, currentUserAccessor.GetCurrentUser());
        if (string.IsNullOrWhiteSpace(assessment.AssessedBy))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AssessedBy"] = ["AssessedBy is required"]
            });
        }

        await repository.CreateAsync(assessment, cancellationToken);
        await eventLogger.LogAsync(EntityType, assessment.Id, Describe(assessment), EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-condition-assessments/{assessment.Id}", AssetConditionAssessmentResponse.FromEntity(assessment));
    }

    private static async Task<IResult> UpdateAssessmentAsync(
        string id,
        UpdateAssetConditionAssessmentRequest request,
        IAssetConditionAssessmentRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var assessment = await repository.GetByIdAsync(id, cancellationToken);
        if (assessment is null || assessment.IsDeleted)
        {
            return AssessmentNotFound(id);
        }

        request.ApplyTo(assessment, currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(id, assessment, cancellationToken))
        {
            return AssessmentNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, assessment.Id, Describe(assessment), EventAction.Updated, cancellationToken);

        return Results.Ok(AssetConditionAssessmentResponse.FromEntity(assessment));
    }

    private static string Describe(AssetConditionAssessment assessment) =>
        $"{assessment.AssetRegistrationId} - {assessment.AssessmentDate:dd MMM yyyy} - {assessment.OverallCondition}";

    private static IResult AssessmentNotFound(string id)
    {
        return Results.NotFound(new { message = $"Condition assessment '{id}' was not found." });
    }
}
