using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Documents filed against registered assets, for the Asset Documents screen.</summary>
public static class AssetDocumentEndpoints
{
    private const string EntityType = "AssetDocument";

    public static RouteGroupBuilder MapAssetDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-documents").WithTags("Asset Documents");

        group.MapGet("", GetDocumentsAsync).WithName("GetAssetDocuments");
        group.MapGet("/{id}", GetDocumentByIdAsync).WithName("GetAssetDocumentById");
        group.MapPost("", CreateDocumentsAsync).WithName("CreateAssetDocuments");
        group.MapPut("/{id}", UpdateDocumentAsync).WithName("UpdateAssetDocument");
        group.MapDelete("/{id}", DeleteDocumentAsync).WithName("DeleteAssetDocument");
        group.MapPost("/bulk-delete", DeleteDocumentsAsync).WithName("DeleteAssetDocuments");

        return group;
    }

    private static async Task<IResult> GetDocumentsAsync(
        IAssetDocumentRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? assetRegistrationId = null,
        string? documentType = null,
        string? lifecycleStage = null,
        string? status = null,
        int page = 1,
        int pageSize = AssetDocumentRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetDocumentRepository.MaxPageSize ? AssetDocumentRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetDocumentQuery(search, assetRegistrationId, documentType, lifecycleStage, status, page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetDocumentListResponse(
            result.Items.Select(AssetDocumentResponse.FromEntity).ToList(),
            result.TotalCount,
            page,
            pageSize));
    }

    private static async Task<IResult> GetDocumentByIdAsync(
        string id,
        IAssetDocumentRepository repository,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);

        return document is null || document.IsDeleted
            ? DocumentNotFound(id)
            : Results.Ok(AssetDocumentResponse.FromEntity(document));
    }

    /// <summary>Saves one document per selected asset, all pointing at the same uploaded file.</summary>
    private static async Task<IResult> CreateDocumentsAsync(
        CreateAssetDocumentRequest request,
        IAssetDocumentRepository repository,
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

        var assets = new List<AssetRegistration>();
        foreach (var assetRegistrationId in ContractText.CleanList(request.AssetRegistrationIds, StringComparer.Ordinal))
        {
            var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
            if (asset is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["AssetRegistrationIds"] = [$"Asset '{assetRegistrationId}' was not found"]
                });
            }

            assets.Add(asset);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var created = new List<AssetDocumentResponse>();

        foreach (var asset in assets)
        {
            var document = request.ToEntity(asset, currentUser);
            await repository.CreateAsync(document, cancellationToken);
            await eventLogger.LogAsync(EntityType, document.Id, $"{asset.AssetRegistrationId} - {document.DocumentName}", EventAction.Created, cancellationToken);
            created.Add(AssetDocumentResponse.FromEntity(document));
        }

        return Results.Created("/api/asset-documents", created);
    }

    private static async Task<IResult> UpdateDocumentAsync(
        string id,
        UpdateAssetDocumentRequest request,
        IAssetDocumentRepository repository,
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

        var document = await repository.GetByIdAsync(id, cancellationToken);
        if (document is null || document.IsDeleted)
        {
            return DocumentNotFound(id);
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

        request.ApplyTo(document, asset, currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(id, document, cancellationToken))
        {
            return DocumentNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, document.Id, $"{asset.AssetRegistrationId} - {document.DocumentName}", EventAction.Updated, cancellationToken);

        return Results.Ok(AssetDocumentResponse.FromEntity(document));
    }

    private static async Task<IResult> DeleteDocumentAsync(
        string id,
        IAssetDocumentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        if (document is null || document.IsDeleted || !await repository.DeleteAsync(id, cancellationToken))
        {
            return DocumentNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, document.Id, $"{document.AssetRegistrationId} - {document.DocumentName}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Deletes the documents ticked in the list. Ids that are not found are skipped.</summary>
    private static async Task<IResult> DeleteDocumentsAsync(
        DeleteAssetDocumentsRequest request,
        IAssetDocumentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var ids = ContractText.CleanList(request.Ids, StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Ids"] = ["Select at least one document"]
            });
        }

        var deleted = 0;
        foreach (var id in ids)
        {
            var document = await repository.GetByIdAsync(id, cancellationToken);
            if (document is null || document.IsDeleted || !await repository.DeleteAsync(id, cancellationToken))
            {
                continue;
            }

            await eventLogger.LogAsync(EntityType, document.Id, $"{document.AssetRegistrationId} - {document.DocumentName}", EventAction.Deleted, cancellationToken);
            deleted++;
        }

        return Results.Ok(new { deletedCount = deleted });
    }

    private static IResult DocumentNotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset document '{id}' was not found." });
    }
}
