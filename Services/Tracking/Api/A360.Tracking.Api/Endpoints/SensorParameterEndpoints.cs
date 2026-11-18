using System.Text.RegularExpressions;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace A360.Tracking.Api.Endpoints;

/// <summary>A measurement parameter added in Sensor Management › Measurement Parameters (collection SensorParameter).</summary>
[BsonIgnoreExtraElements]
public sealed class SensorParameterDocument
{
    public const string CollectionName = "SensorParameter";

    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>e.g. Dissolved Oxygen.</summary>
    [BsonElement("measured_parameter")]
    public string MeasuredParameter { get; set; } = string.Empty;

    [BsonElement("unit")]
    public string? Unit { get; set; }

    /// <summary>e.g. DO Probe; the sensor type sensors of this parameter are created with.</summary>
    [BsonElement("sensor_type")]
    public string SensorType { get; set; } = string.Empty;

    /// <summary>Sensor id prefix, e.g. SEN-DO.</summary>
    [BsonElement("id_prefix")]
    public string IdPrefix { get; set; } = "SEN";

    [BsonElement("make_model")]
    public string? MakeModel { get; set; }

    /// <summary>e.g. 0 – 20 mg/L.</summary>
    [BsonElement("measuring_range")]
    public string? MeasuringRange { get; set; }

    /// <summary>e.g. ±0.1 mg/L.</summary>
    [BsonElement("accuracy")]
    public string? Accuracy { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("created_by")]
    public string? CreatedBy { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("client_id")]
    public string? ClientId { get; set; }

    [BsonElement("tenant_id")]
    public string? TenantId { get; set; }

    [BsonElement("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>A sensor type / measurement parameter: the built-in ones plus those added by users.</summary>
/// <param name="Custom">Added by a user (can be deleted); built-in ones cannot.</param>
public sealed record SensorParameterOption(
    string SensorType, string IdPrefix, string MeasuredParameter, string Unit, string? MakeModel, string? MeasuringRange, string? Accuracy,
    string? Description, bool Custom, string? Id, string? CreatedBy, DateTime? CreatedAt);

/// <summary>Add Parameter popup body.</summary>
public sealed record CreateSensorParameterRequest(
    string? MeasuredParameter, string? Unit, string? SensorType, string? IdPrefix, string? MakeModel, string? MeasuringRange, string? Accuracy,
    string? Description, string? ClientId, string? TenantId);

/// <summary>Reads / writes the measurement parameters added by users.</summary>
public sealed class SensorParameterStore(IMongoDatabase database)
{
    private IMongoCollection<SensorParameterDocument> Collection => database.GetCollection<SensorParameterDocument>(SensorParameterDocument.CollectionName);

    public Task<List<SensorParameterDocument>> ListCustomAsync(CancellationToken ct) =>
        Collection.Find(p => !p.IsDeleted).SortBy(p => p.MeasuredParameter).ToListAsync(ct);

    /// <summary>Built-in sensor types first, then the user's.</summary>
    public async Task<List<SensorParameterOption>> AllAsync(CancellationToken ct)
    {
        var builtIn = SensorTypeOption.All.Select(o => new SensorParameterOption(o.SensorType, o.IdPrefix, o.MeasuredParameter, o.Unit, null, null, null, null, false, null, null, null));
        var custom = (await ListCustomAsync(ct)).Select(ToOption);
        return [.. builtIn, .. custom];
    }

    public static SensorParameterOption ToOption(SensorParameterDocument p) =>
        new(p.SensorType, p.IdPrefix, p.MeasuredParameter, p.Unit ?? string.Empty, p.MakeModel, p.MeasuringRange, p.Accuracy, p.Description, true, p.Id, p.CreatedBy, p.CreatedAt);

    public Task InsertAsync(SensorParameterDocument p, CancellationToken ct) => Collection.InsertOneAsync(p, cancellationToken: ct);

    public async Task<SensorParameterDocument?> GetAsync(string id, CancellationToken ct) =>
        ObjectId.TryParse(id, out _) ? await Collection.Find(p => p.Id == id && !p.IsDeleted).FirstOrDefaultAsync(ct) : null;

    public async Task<bool> DeleteAsync(string id, CancellationToken ct) =>
        (await Collection.UpdateOneAsync(p => p.Id == id && !p.IsDeleted, Builders<SensorParameterDocument>.Update.Set(p => p.IsDeleted, true), cancellationToken: ct)).ModifiedCount > 0;
}

/// <summary>Sensor Management › Measurement Parameters: list, add and delete parameters (built-in ones are read-only).</summary>
public static class SensorParameterEndpoints
{
    private const string EntityType = "SensorParameter";
    private const int TextMax = 60;
    private const int DetailMax = 100;

    public static RouteGroupBuilder MapSensorParameterEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sensor-parameters").WithTags("Sensors");

        group.MapGet("", async (SensorParameterStore store, CancellationToken ct) => Results.Ok(await store.AllAsync(ct))).WithName("GetSensorParameters");
        group.MapPost("", CreateAsync).WithName("CreateSensorParameter");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteSensorParameter");

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateSensorParameterRequest request,
        SensorParameterStore store,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        Text(errors, "MeasuredParameter", request.MeasuredParameter, TextMax, required: true);
        Text(errors, "SensorType", request.SensorType, TextMax, required: true);
        Text(errors, "Unit", request.Unit, 20, required: false);
        Text(errors, "IdPrefix", request.IdPrefix, 12, required: false);
        Text(errors, "MakeModel", request.MakeModel, DetailMax, required: false);
        Text(errors, "MeasuringRange", request.MeasuringRange, DetailMax, required: false);
        Text(errors, "Accuracy", request.Accuracy, DetailMax, required: false);
        Text(errors, "Description", request.Description, 500, required: false);
        if (!string.IsNullOrWhiteSpace(request.IdPrefix) && !Regex.IsMatch(request.IdPrefix.Trim(), "^[A-Za-z0-9-]+$"))
        {
            errors["IdPrefix"] = ["IdPrefix may contain only letters, digits and dashes"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var parameter = request.MeasuredParameter!.Trim();
        var type = request.SensorType!.Trim();
        var all = await store.AllAsync(ct);
        if (all.Any(o => string.Equals(o.MeasuredParameter, parameter, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Conflict(new { message = $"The parameter '{parameter}' already exists." });
        }

        if (all.Any(o => string.Equals(o.SensorType, type, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Conflict(new { message = $"The sensor type '{type}' is already used by another parameter." });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var doc = new SensorParameterDocument
        {
            MeasuredParameter = parameter,
            Unit = ContractText(request.Unit),
            SensorType = type,
            IdPrefix = ContractText(request.IdPrefix)?.ToUpperInvariant() ?? DefaultPrefix(type),
            MakeModel = ContractText(request.MakeModel),
            MeasuringRange = ContractText(request.MeasuringRange),
            Accuracy = ContractText(request.Accuracy),
            Description = ContractText(request.Description),
            CreatedBy = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText(request.ClientId) ?? currentUser?.ClientId,
            TenantId = ContractText(request.TenantId) ?? currentUser?.TenantId
        };

        await store.InsertAsync(doc, ct);
        await eventLogger.LogAsync(EntityType, doc.Id!, $"{doc.MeasuredParameter} ({doc.SensorType})", EventAction.Created, ct);
        return Results.Created($"/api/sensor-parameters/{doc.Id}", SensorParameterStore.ToOption(doc));
    }

    /// <summary>Only parameters no sensor uses can be deleted.</summary>
    private static async Task<IResult> DeleteAsync(string id, SensorParameterStore store, ISensorRepository sensors, IEventLogger eventLogger, CancellationToken ct)
    {
        var doc = await store.GetAsync(id.Trim(), ct);
        if (doc is null)
        {
            return Results.NotFound(new { message = $"Measurement parameter '{id}' was not found." });
        }

        var used = (await sensors.GetAllAsync(ct)).Count(s => !s.IsDeleted && string.Equals(s.SensorType, doc.SensorType, StringComparison.OrdinalIgnoreCase));
        if (used > 0)
        {
            return Results.Conflict(new { message = $"'{doc.MeasuredParameter}' is used by {used} sensor(s). Reassign or delete them first." });
        }

        await store.DeleteAsync(doc.Id!, ct);
        await eventLogger.LogAsync(EntityType, doc.Id!, $"{doc.MeasuredParameter} ({doc.SensorType})", EventAction.Deleted, ct);
        return Results.NoContent();
    }

    /// <summary>"Dissolved Oxygen Probe" → SEN-DOP.</summary>
    private static string DefaultPrefix(string type)
    {
        var letters = string.Concat(type.Split(' ', '-', '_').Where(w => w.Length > 0 && char.IsLetterOrDigit(w[0])).Select(w => char.ToUpperInvariant(w[0])));
        return $"SEN-{(letters.Length == 0 ? "X" : letters[..Math.Min(4, letters.Length)])}";
    }

    private static string? ContractText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Text(Dictionary<string, string[]> errors, string field, string? value, int max, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors[field] = [$"{field} is required"];
            }
        }
        else if (value.Trim().Length > max)
        {
            errors[field] = [$"{field} cannot be longer than {max} characters"];
        }
    }
}
