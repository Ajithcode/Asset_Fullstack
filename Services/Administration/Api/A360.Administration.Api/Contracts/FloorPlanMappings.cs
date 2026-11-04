using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class FloorPlanMappings
{
    public const string DefaultVersion = "v1.0";

    public static FloorPlan ToEntity(this CreateFloorPlanRequest request, CurrentUser? currentUser)
    {
        var now = DateTime.UtcNow;
        var createdBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName);
        var status = FloorPlanValidationExtensions.Normalize(request.Status, FloorPlanStatus.All) ?? FloorPlanStatus.Draft;
        var published = status == FloorPlanStatus.Published;

        return new FloorPlan
        {
            // Generated on the server so every floor plan gets a stable id independent of the Mongo _id.
            FloorPlanId = Guid.NewGuid().ToString(),
            Name = request.Name!.Trim(),
            Description = Optional(request.Description),
            LocationId = Optional(request.LocationId),
            LocationPath = request.LocationPath!.Trim(),
            Type = FloorPlanValidationExtensions.Normalize(request.Type, FloorPlanType.All)!,
            FileName = request.FileName!.Trim(),
            FileUrl = Optional(request.FileUrl),
            FileFormat = FloorPlanValidationExtensions.FileFormatOf(request.FileFormat, request.FileName),
            FileSizeBytes = request.FileSizeBytes,
            Version = FloorPlanValidationExtensions.NormalizeVersion(request.Version) ?? DefaultVersion,
            Status = status,
            Width = Round(request.Width),
            Length = Round(request.Length),
            Scale = request.Scale,
            // A plan created as Published is published now by the creator unless the request says otherwise.
            PublishedOn = published ? request.PublishedOn?.ToUniversalTime() ?? now : null,
            PublishedBy = published ? FirstNonEmpty(request.PublishedBy, createdBy) : null,
            CreatedBy = createdBy,
            CreatedAt = now,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    private static double? Round(double? value)
    {
        return value is null ? null : Math.Round(value.Value, 2);
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
