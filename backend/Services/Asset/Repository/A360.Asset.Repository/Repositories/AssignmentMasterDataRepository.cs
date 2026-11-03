using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace A360.Asset.Repository.Repositories;

/// <summary>Finds employees, departments and places by name or code, so rows of a bulk assignment file can be matched.</summary>
public interface IAssignmentMasterDataRepository
{
    Task<IReadOnlyCollection<AssignmentEmployeeLookup>> FindEmployeesAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssignmentDepartmentLookup>> FindDepartmentsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssignmentSiteLookup>> FindSitesAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssignmentBuildingLookup>> FindBuildingsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssignmentFloorLookup>> FindFloorsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssignmentLocationLookup>> FindLocationsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only view of the MasterDataManagement collections, which share this service's database.
/// Only the fields needed for matching are read; deleted records are left out.
/// </summary>
public sealed class AssignmentMasterDataRepository(IMongoDatabase database) : IAssignmentMasterDataRepository
{
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public Task<IReadOnlyCollection<AssignmentEmployeeLookup>> FindEmployeesAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default) =>
        FindAsync<AssignmentEmployeeLookup>("master_employees", "employee_name", "employee_id", namesOrCodes, cancellationToken);

    public Task<IReadOnlyCollection<AssignmentDepartmentLookup>> FindDepartmentsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default) =>
        FindAsync<AssignmentDepartmentLookup>("master_departments", "department_name", "code", namesOrCodes, cancellationToken);

    public Task<IReadOnlyCollection<AssignmentSiteLookup>> FindSitesAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default) =>
        FindAsync<AssignmentSiteLookup>("master_sites", "site_name", "code", namesOrCodes, cancellationToken);

    public Task<IReadOnlyCollection<AssignmentBuildingLookup>> FindBuildingsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default) =>
        FindAsync<AssignmentBuildingLookup>("master_buildings", "name", "code", namesOrCodes, cancellationToken);

    public Task<IReadOnlyCollection<AssignmentFloorLookup>> FindFloorsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default) =>
        FindAsync<AssignmentFloorLookup>("master_floors", "name", "code", namesOrCodes, cancellationToken);

    public Task<IReadOnlyCollection<AssignmentLocationLookup>> FindLocationsAsync(IReadOnlyCollection<string> namesOrCodes, CancellationToken cancellationToken = default) =>
        FindAsync<AssignmentLocationLookup>("master_locations", "location_name", "code", namesOrCodes, cancellationToken);

    private async Task<IReadOnlyCollection<TLookup>> FindAsync<TLookup>(
        string collectionName,
        string nameField,
        string codeField,
        IReadOnlyCollection<string> namesOrCodes,
        CancellationToken cancellationToken)
    {
        if (namesOrCodes.Count == 0)
        {
            return [];
        }

        var builder = Builders<TLookup>.Filter;
        var filter = builder.And(
            builder.Ne("is_deleted", true),
            builder.Or(builder.In(nameField, namesOrCodes), builder.In(codeField, namesOrCodes)));

        return await database
            .GetCollection<TLookup>(collectionName)
            .Find(filter, new FindOptions { Collation = CaseInsensitive })
            .ToListAsync(cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public sealed class AssignmentLookupReference
{
    [BsonElement("id")]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssignmentEmployeeLookup
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    // The organisation's employee number, e.g. "EMP-0042".
    [BsonElement("employee_id")]
    public string? EmployeeCode { get; set; }

    [BsonElement("employee_name")]
    public string? EmployeeName { get; set; }

    [BsonElement("designation")]
    public string? Designation { get; set; }

    [BsonElement("department")]
    public AssignmentLookupReference? Department { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssignmentDepartmentLookup
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("department_name")]
    public string? DepartmentName { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("department_head")]
    public AssignmentLookupReference? DepartmentHead { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssignmentSiteLookup
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssignmentBuildingLookup
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("site")]
    public AssignmentLookupReference? Site { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssignmentFloorLookup
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("site")]
    public AssignmentLookupReference? Site { get; set; }

    [BsonElement("building_id")]
    public string? BuildingId { get; set; }

    [BsonElement("building_name")]
    public string? BuildingName { get; set; }
}

/// <summary>A room, area or other named place, e.g. "Meeting Room 1".</summary>
[BsonIgnoreExtraElements]
public sealed class AssignmentLocationLookup
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("location_name")]
    public string? LocationName { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("building")]
    public string? Building { get; set; }

    [BsonElement("floor")]
    public string? Floor { get; set; }
}
