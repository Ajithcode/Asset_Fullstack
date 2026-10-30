using StandardEntity = A360.MasterDataManagement.Domain.Entities.Standard;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateStandardRequest(
    string? StandardName,
    string? Code,
    string? Organization,
    string? Category,
    string? Version,
    string? Description,
    string? Title,
    DateOnly? PublishedOn,
    DateOnly? NextReview,
    string? Url,
    bool? Mandatory,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public StandardEntity ToEntity()
    {
        return new StandardEntity
        {
            StandardName = StandardName!.Trim(),
            Code = Code!.Trim(),
            Organization = Organization!.Trim(),
            Category = Category!.Trim(),
            Version = string.IsNullOrWhiteSpace(Version) ? null : Version.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
            PublishedOn = PublishedOn,
            NextReview = NextReview,
            Url = string.IsNullOrWhiteSpace(Url) ? null : Url.Trim(),
            Mandatory = Mandatory ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateStandardRequest(
    string? StandardName,
    string? Code,
    string? Organization,
    string? Category,
    string? Version,
    string? Description,
    string? Title,
    DateOnly? PublishedOn,
    DateOnly? NextReview,
    string? Url,
    bool? Mandatory,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(StandardEntity standard)
    {
        standard.StandardName = StandardName!.Trim();
        standard.Code = Code!.Trim();
        standard.Organization = Organization!.Trim();
        standard.Category = Category!.Trim();
        standard.Version = string.IsNullOrWhiteSpace(Version) ? null : Version.Trim();
        standard.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        standard.Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim();
        standard.PublishedOn = PublishedOn;
        standard.NextReview = NextReview;
        standard.Url = string.IsNullOrWhiteSpace(Url) ? null : Url.Trim();
        standard.Mandatory = Mandatory ?? false;

        standard.UpdatedBy = UpdatedBy;
        standard.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            standard.Status = Status;
        }
    }
}

public sealed record UpdateStandardStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record StandardResponse(
    string Id,
    string StandardName,
    string Code,
    string Organization,
    string Category,
    string? Version,
    string? Description,
    string? Title,
    DateOnly? PublishedOn,
    DateOnly? NextReview,
    string? Url,
    bool Mandatory,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static StandardResponse FromEntity(StandardEntity standard)
    {
        return new StandardResponse(
            standard.Id ?? string.Empty,
            standard.StandardName ?? string.Empty,
            standard.Code ?? string.Empty,
            standard.Organization ?? string.Empty,
            standard.Category ?? string.Empty,
            standard.Version,
            standard.Description,
            standard.Title,
            standard.PublishedOn,
            standard.NextReview,
            standard.Url,
            standard.Mandatory,
            standard.CreatedBy,
            standard.CreatedAt,
            standard.UpdatedBy,
            standard.UpdatedAt,
            standard.ClientId,
            standard.TenantId,
            standard.Status,
            standard.IsDeleted
        );
    }
}
