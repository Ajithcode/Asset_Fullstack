using MongoDB.Bson;

namespace A360.Repository.Repositories;

public static class MongoObjectId
{
    public static bool IsValid(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) && ObjectId.TryParse(id, out _);
    }

    public static string Create()
    {
        return ObjectId.GenerateNewId().ToString();
    }
}
