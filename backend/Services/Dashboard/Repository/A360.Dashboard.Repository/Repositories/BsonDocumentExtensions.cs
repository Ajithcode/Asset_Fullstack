using MongoDB.Bson;

namespace A360.Dashboard.Repository.Repositories;

internal static class BsonDocumentExtensions
{
    public static string? GetStringOrNull(this BsonDocument? document, string name)
    {
        return document is not null && document.TryGetValue(name, out var value) && value.IsString
            ? value.AsString
            : null;
    }
}
