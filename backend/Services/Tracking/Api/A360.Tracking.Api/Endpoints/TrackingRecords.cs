using A360.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Steps shared by the list and update endpoints of every Tracking list.</summary>
internal static class TrackingRecords
{
    /// <summary>Records that are not deleted, newest first.</summary>
    public static IEnumerable<TEntity> Listed<TEntity>(IEnumerable<TEntity> records)
        where TEntity : BaseEntity
    {
        return records
            .Where(record => !record.IsDeleted)
            .OrderByDescending(record => record.CreatedAt);
    }

    /// <summary>
    /// An update rebuilds the record from the request (the same body as create);
    /// this keeps what the request cannot change and stamps who changed it.
    /// </summary>
    public static TEntity Replacing<TEntity>(this TEntity updated, TEntity existing, CurrentUser? currentUser)
        where TEntity : BaseEntity
    {
        updated.Id = existing.Id;
        updated.CreatedBy = existing.CreatedBy;
        updated.CreatedAt = existing.CreatedAt;
        updated.ClientId = existing.ClientId;
        updated.TenantId = existing.TenantId;
        updated.UpdatedBy = currentUser?.UserName;
        updated.UpdatedAt = DateTime.UtcNow;
        return updated;
    }

    /// <summary>The key in the URL names the record, so the body cannot change it to another key.</summary>
    public static IResult? KeyChanged(string field, string urlKey, string? bodyKey)
    {
        return string.Equals(urlKey.Trim(), bodyKey?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [field] = [$"{field} cannot be changed; it must match the {field} in the URL"]
            });
    }
}
