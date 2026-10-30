using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Registers a tag with the Tag IDs list columns: Tag ID, Type, Technology, Linked Asset, Site / Location, Assignment Status, Last Seen.</summary>
/// <param name="Status">Assignment status, one of <see cref="TagStatus.All"/>; defaults to Assigned when an asset is linked, otherwise Available.</param>
public sealed record CreateTrackingTagRequest(
    string? TagId,
    string? TagType,
    string? Technology,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteId,
    string? SiteName,
    string? LocationName,
    string? Status,
    DateTime? LastSeenAt,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public TrackingTag ToEntity(CurrentUser? currentUser)
    {
        var assetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId);

        return new TrackingTag
        {
            TagId = TagId!.Trim(),
            TagType = TagType!.Trim(),
            Technology = ContractText.Canonical(Technology, TagTechnology.All)!,
            AssetRegistrationId = assetRegistrationId,
            AssetName = ContractText.TrimOrNull(AssetName),
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = ContractText.TrimOrNull(SiteName),
            LocationName = ContractText.TrimOrNull(LocationName),
            Status = ContractText.Canonical(Status, TagStatus.All)
                ?? (assetRegistrationId is null ? TagStatus.Available : TagStatus.Assigned),
            LastSeenAt = LastSeenAt?.ToUniversalTime(),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record TrackingTagResponse(
    string Id,
    string TagId,
    string TagType,
    string Technology,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteId,
    string? SiteName,
    string? LocationName,
    string? Status,
    DateTime? LastSeenAt,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? ClientId,
    string? TenantId)
{
    public static TrackingTagResponse FromEntity(TrackingTag tag)
    {
        return new TrackingTagResponse(
            tag.Id,
            tag.TagId,
            tag.TagType,
            tag.Technology,
            tag.AssetRegistrationId,
            tag.AssetName,
            tag.SiteId,
            tag.SiteName,
            tag.LocationName,
            tag.Status,
            tag.LastSeenAt,
            tag.CreatedBy,
            tag.CreatedAt,
            tag.ClientId,
            tag.TenantId);
    }
}

/// <summary>An option of the Technology dropdown, with the tag types it offers and its tag id prefix.</summary>
public sealed record TagTechnologyOptionResponse(string Technology, string IdPrefix, IReadOnlyList<string> TagTypes)
{
    public static TagTechnologyOptionResponse FromTechnology(string technology)
    {
        return new TagTechnologyOptionResponse(technology, TagTechnology.IdPrefixes[technology], TagTechnology.TagTypes[technology]);
    }
}

internal static class ContractText
{
    public static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }

    /// <summary>The allowed value matching the input ignoring case (e.g. "passive rfid" → "Passive RFID"), or null.</summary>
    public static string? Canonical(string? value, IEnumerable<string> allowed)
    {
        var trimmed = TrimOrNull(value);
        return trimmed is null ? null : allowed.FirstOrDefault(option => string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
