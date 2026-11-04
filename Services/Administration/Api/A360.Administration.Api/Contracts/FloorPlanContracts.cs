using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateFloorPlanRequest(
    string? Name,
    string? Description,
    string? LocationId,
    string? LocationPath,
    string? Type,
    string? FileName,
    string? FileUrl,
    string? FileFormat,
    long? FileSizeBytes,
    string? Version,
    string? Status,
    double? Width,
    double? Length,
    int? Scale,
    DateTime? PublishedOn,
    string? PublishedBy,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record FloorPlanResponse(
    string FloorPlanId,
    string Name,
    string? Description,
    string? LocationId,
    string LocationPath,
    string Type,
    string FileName,
    string? FileUrl,
    string? FileFormat,
    long? FileSizeBytes,
    string Version,
    string? Status,
    double? Width,
    double? Length,
    // Display text for the details panel, e.g. "60.00 m × 32.00 m" and "1 : 100".
    string? Dimensions,
    int? Scale,
    string? ScaleText,
    DateTime? PublishedOn,
    string? PublishedBy,
    DateTime? LastUpdated,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? TenantId,
    string? ClientId)
{
    public static FloorPlanResponse FromEntity(FloorPlan plan)
    {
        return new FloorPlanResponse(
            plan.FloorPlanId,
            plan.Name,
            plan.Description,
            plan.LocationId,
            plan.LocationPath,
            plan.Type,
            plan.FileName,
            plan.FileUrl,
            plan.FileFormat,
            plan.FileSizeBytes,
            plan.Version,
            plan.Status,
            plan.Width,
            plan.Length,
            plan.Width is { } width && plan.Length is { } length ? $"{width:0.00} m × {length:0.00} m" : null,
            plan.Scale,
            plan.Scale is { } scale ? $"1 : {scale}" : null,
            plan.PublishedOn,
            plan.PublishedBy,
            plan.UpdatedAt ?? plan.CreatedAt,
            plan.CreatedBy,
            plan.CreatedAt,
            plan.TenantId,
            plan.ClientId);
    }
}

/// <summary>Choices for the Type and Status dropdowns and the accepted file formats.</summary>
public sealed record FloorPlanOptionsResponse(
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> FileFormats)
{
    public static readonly FloorPlanOptionsResponse Instance =
        new(FloorPlanType.All, FloorPlanStatus.All, FloorPlanFileFormat.All);
}
