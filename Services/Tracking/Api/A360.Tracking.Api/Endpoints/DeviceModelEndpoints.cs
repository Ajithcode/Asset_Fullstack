using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>
/// Device Type & Model: device types (Add Device Type) and model specifications (Add Device Model), with the
/// page cards. Models are addressed by model number (e.g. EM300-TH), device types by name (e.g. Environmental Sensor).
/// </summary>
public static class DeviceModelEndpoints
{
    private const string EntityType = "DeviceModel";
    private const string TypeEntityType = "DeviceType";
    private const int DefaultPageSize = 8;

    public static RouteGroupBuilder MapDeviceModelEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-models").WithTags("Device Models");

        group.MapGet("", GetDeviceModelsAsync).WithName("GetDeviceModels");
        // Literal segments take precedence over /{modelNumber}, so these are not read as model numbers.
        group.MapGet("/summary", GetSummaryAsync).WithName("GetDeviceModelSummary");
        group.MapGet("/device-types", GetDeviceTypesAsync).WithName("GetDeviceModelTypes");
        group.MapGet("/device-types/{name}", GetDeviceTypeAsync).WithName("GetDeviceType");
        group.MapPost("/device-types", CreateDeviceTypeAsync).WithName("CreateDeviceType");
        group.MapPut("/device-types/{name}", UpdateDeviceTypeAsync).WithName("UpdateDeviceType");
        group.MapDelete("/device-types/{name}", DeleteDeviceTypeAsync).WithName("DeleteDeviceType");
        group.MapGet("/device-type-icons", () => Results.Ok(DeviceTypeIcon.All)).WithName("GetDeviceTypeIcons");
        group.MapGet("/thumbnail-styles", () => Results.Ok(DeviceTypeThumbnail.All)).WithName("GetDeviceTypeThumbnailStyles");
        group.MapGet("/connectivities", () => Results.Ok(DeviceModelConnectivity.All)).WithName("GetDeviceModelConnectivities");
        group.MapGet("/power-sources", () => Results.Ok(DeviceModelPowerSource.All)).WithName("GetDeviceModelPowerSources");
        group.MapGet("/measurements", () => Results.Ok(DeviceMeasurement.All)).WithName("GetDeviceModelMeasurements");
        group.MapGet("/configurable-attributes", () => Results.Ok(DeviceModelAttribute.All)).WithName("GetDeviceModelConfigurableAttributes");
        group.MapGet("/statuses", () => Results.Ok(DeviceModelStatus.All)).WithName("GetDeviceModelStatuses");
        group.MapGet("/protocols", GetProtocolsAsync).WithName("GetDeviceModelProtocols");
        group.MapGet("/frequency-bands", GetFrequencyBandsAsync).WithName("GetDeviceModelFrequencyBands");
        group.MapGet("/ip-ratings", GetIpRatingsAsync).WithName("GetDeviceModelIpRatings");
        group.MapGet("/manufacturers", GetManufacturersAsync).WithName("GetDeviceModelManufacturers");
        group.MapGet("/form-options", GetFormOptionsAsync).WithName("GetDeviceModelFormOptions");
        group.MapGet("/{modelNumber}", GetDeviceModelAsync).WithName("GetDeviceModelByModelNumber");
        group.MapPost("", CreateDeviceModelAsync).WithName("CreateDeviceModel");
        group.MapPut("/{modelNumber}", UpdateDeviceModelAsync).WithName("UpdateDeviceModel");
        group.MapDelete("/{modelNumber}", DeleteDeviceModelAsync).WithName("DeleteDeviceModel");

        return group;
    }

    /// <summary>The model table, filtered and paged.</summary>
    private static async Task<IResult> GetDeviceModelsAsync(
        [AsParameters] DeviceModelListQuery query,
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var search = ContractText.TrimOrNull(query.Search);
        var deviceType = ContractText.TrimOrNull(query.DeviceType);
        var manufacturer = ContractText.TrimOrNull(query.Manufacturer);
        var connectivity = ContractText.Canonical(query.Connectivity, DeviceModelConnectivity.All);
        var status = ContractText.Canonical(query.Status, DeviceModelStatus.All);

        var matching = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken))
            .Where(model =>
                (deviceType is null || Same(model.DeviceType, deviceType))
                && (manufacturer is null || Same(model.Manufacturer, manufacturer))
                && (connectivity is null || model.Connectivity == connectivity)
                && (status is null || model.Status == status)
                && (search is null || new[] { model.ModelNumber, model.Manufacturer, model.DeviceType, model.Protocol }
                    .Any(field => field?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)))
            .ToList();

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;
        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(DeviceModelListItemResponse.FromEntity)
            .ToList();

        return Results.Ok(PagedResponse<DeviceModelListItemResponse>.Of(items, matching.Count, page, pageSize));
    }

    /// <summary>Device Types, Models, Active Models and Pending Review cards.</summary>
    private static async Task<IResult> GetSummaryAsync(
        IDeviceModelRepository repository,
        IDeviceTypeRepository types,
        CancellationToken cancellationToken)
    {
        var models = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken)).ToList();
        var deviceTypes = await types.GetActiveAsync(cancellationToken);

        return Results.Ok(new DeviceModelSummaryResponse(
            deviceTypes.Count,
            models.Count,
            models.Count(model => model.Status == DeviceModelStatus.Active),
            models.Count(model => model.Status is DeviceModelStatus.Draft or DeviceModelStatus.PendingReview)));
    }

    /// <summary>The Device Type list, each with its model count.</summary>
    private static async Task<IResult> GetDeviceTypesAsync(
        IDeviceModelRepository repository,
        IDeviceTypeRepository types,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await DeviceTypesWithCountsAsync(repository, types, cancellationToken));
    }

    /// <summary>One device type with its Model Capability Overview.</summary>
    private static async Task<IResult> GetDeviceTypeAsync(
        string name,
        IDeviceModelRepository repository,
        IDeviceTypeRepository types,
        CancellationToken cancellationToken)
    {
        var type = await types.GetByNameAsync(name.Trim(), cancellationToken);
        if (type is null)
        {
            return TypeNotFound(name);
        }

        var models = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken))
            .Where(model => Same(model.DeviceType, type.Name))
            .ToList();

        static IReadOnlyList<CapabilityCountResponse> Count(
            IEnumerable<string> options,
            IReadOnlyList<DeviceModel> models,
            Func<DeviceModel, IEnumerable<string>> values,
            Func<string, string?> unitOf)
        {
            var used = models.SelectMany(model => values(model).Distinct(StringComparer.OrdinalIgnoreCase)).ToList();
            return options
                .Concat(used)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(option => new CapabilityCountResponse(option, unitOf(option), used.Count(value => Same(value, option))))
                .ToList();
        }

        return Results.Ok(new DeviceTypeDetailsResponse(
            DeviceTypeResponse.FromEntity(type, models),
            Count(DeviceModelConnectivity.All, models, model => [model.Connectivity], _ => null),
            Count(DeviceMeasurement.All.Select(m => m.Name), models, model => model.SupportedMeasurements,
                option => DeviceMeasurement.All.FirstOrDefault(m => Same(m.Name, option))?.Unit),
            Count(DeviceModelAttribute.All, models, model => model.ConfigurableAttributes, _ => null)));
    }

    /// <summary>The Add Device Type form.</summary>
    private static async Task<IResult> CreateDeviceTypeAsync(
        CreateDeviceTypeRequest request,
        IDeviceTypeRepository types,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var name = request.Name!.Trim();
        if (await types.NameExistsAsync(name, cancellationToken))
        {
            return Results.Conflict(new { message = $"Device type '{name}' already exists." });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var type = new DeviceType
        {
            Name = name,
            Icon = ContractText.Canonical(request.Icon, DeviceTypeIcon.All) ?? DeviceTypeIcon.Chip,
            ThumbnailStyle = ContractText.Canonical(request.ThumbnailStyle, DeviceTypeThumbnail.All) ?? DeviceTypeThumbnail.SensorBox,
            Description = ContractText.TrimOrNull(request.Description),
            CreatedBy = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            IsDeleted = false
        };

        await types.CreateAsync(type, cancellationToken);
        await eventLogger.LogAsync(TypeEntityType, type.Name, type.Name, EventAction.Created, cancellationToken);

        return Results.Created($"/api/device-models/device-types/{Uri.EscapeDataString(type.Name)}", DeviceTypeResponse.FromEntity(type, []));
    }

    /// <summary>Changes icon, thumbnail and description; the name names the type, so it cannot change.</summary>
    private static async Task<IResult> UpdateDeviceTypeAsync(
        string name,
        CreateDeviceTypeRequest request,
        IDeviceTypeRepository types,
        IDeviceModelRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (TrackingRecords.KeyChanged("Name", name, request.Name) is { } keyChanged)
        {
            return keyChanged;
        }

        var type = await types.GetByNameAsync(name.Trim(), cancellationToken);
        if (type is null)
        {
            return TypeNotFound(name);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        type.Icon = ContractText.Canonical(request.Icon, DeviceTypeIcon.All) ?? type.Icon;
        type.ThumbnailStyle = ContractText.Canonical(request.ThumbnailStyle, DeviceTypeThumbnail.All) ?? type.ThumbnailStyle;
        type.Description = ContractText.TrimOrNull(request.Description);
        type.UpdatedBy = currentUser?.UserName;
        type.UpdatedAt = DateTime.UtcNow;

        await types.UpdateAsync(type.Id, type, cancellationToken);
        await eventLogger.LogAsync(TypeEntityType, type.Name, type.Name, EventAction.Updated, cancellationToken);

        var models = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken))
            .Where(model => Same(model.DeviceType, type.Name))
            .ToList();
        return Results.Ok(DeviceTypeResponse.FromEntity(type, models));
    }

    /// <summary>Deletes a device type that no model uses.</summary>
    private static async Task<IResult> DeleteDeviceTypeAsync(
        string name,
        IDeviceTypeRepository types,
        IDeviceModelRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var type = await types.GetByNameAsync(name.Trim(), cancellationToken);
        if (type is null)
        {
            return TypeNotFound(name);
        }

        var modelCount = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken))
            .Count(model => Same(model.DeviceType, type.Name));
        if (modelCount > 0)
        {
            return Results.Conflict(new { message = $"Device type '{type.Name}' has {modelCount} model(s); move or delete them first." });
        }

        await types.DeleteAsync(type.Id, cancellationToken);
        await eventLogger.LogAsync(TypeEntityType, type.Name, type.Name, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<DeviceTypeResponse>> DeviceTypesWithCountsAsync(
        IDeviceModelRepository repository,
        IDeviceTypeRepository types,
        CancellationToken cancellationToken)
    {
        var models = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken)).ToList();
        return (await types.GetActiveAsync(cancellationToken))
            .Select(type => DeviceTypeResponse.FromEntity(type, models.Where(model => Same(model.DeviceType, type.Name)).ToList()))
            .ToList();
    }

    private static bool Same(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Protocol suggestions: the standard ones for the connectivity, then any other already used by a model.</summary>
    private static async Task<IResult> GetProtocolsAsync(
        string? connectivity,
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(Suggestions(DeviceModelProtocol.ByConnectivity, connectivity, TrackingRecords.Listed(models), model => [model.Protocol]));
    }

    /// <summary>Frequency band suggestions: the standard ones for the connectivity, then any other already used by a model.</summary>
    private static async Task<IResult> GetFrequencyBandsAsync(
        string? connectivity,
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(Suggestions(DeviceModelFrequencyBand.ByConnectivity, connectivity, TrackingRecords.Listed(models), model => model.FrequencyBands));
    }

    private static async Task<IResult> GetIpRatingsAsync(
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(MergeDistinct(DeviceModelIpRating.All, TrackingRecords.Listed(models).Select(model => model.IpRating)));
    }

    /// <summary>Manufacturers already used by models, for type-ahead. The full master list is GET /api/master-data/manufacturers.</summary>
    private static async Task<IResult> GetManufacturersAsync(
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(MergeDistinct([], TrackingRecords.Listed(models).Select(model => model.Manufacturer)));
    }

    private static async Task<IResult> GetFormOptionsAsync(
        IDeviceModelRepository repository,
        IDeviceTypeRepository types,
        CancellationToken cancellationToken)
    {
        var models = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken)).ToList();

        return Results.Ok(new DeviceModelFormOptionsResponse(
            await DeviceTypesWithCountsAsync(repository, types, cancellationToken),
            MergeDistinct([], models.Select(model => model.Manufacturer)),
            DeviceModelConnectivity.All,
            DeviceModelConnectivity.All.ToDictionary(
                c => c,
                c => Suggestions(DeviceModelProtocol.ByConnectivity, c, models, model => [model.Protocol])),
            DeviceModelConnectivity.All.ToDictionary(
                c => c,
                c => Suggestions(DeviceModelFrequencyBand.ByConnectivity, c, models, model => model.FrequencyBands)),
            DeviceModelPowerSource.All,
            DeviceMeasurement.All,
            MergeDistinct(DeviceModelIpRating.All, models.Select(model => model.IpRating)),
            DeviceModelAttribute.All,
            DeviceModelStatus.All,
            DeviceTypeIcon.All,
            DeviceTypeThumbnail.All));
    }

    /// <summary>
    /// The standard values for one connectivity (or all connectivities when none is given),
    /// followed by values already saved on models of that connectivity.
    /// </summary>
    private static IReadOnlyList<string> Suggestions(
        IReadOnlyDictionary<string, IReadOnlyList<string>> standard,
        string? connectivity,
        IEnumerable<DeviceModel> models,
        Func<DeviceModel, IEnumerable<string?>> used)
    {
        var hasConnectivity = !string.IsNullOrWhiteSpace(connectivity);

        var defaults = hasConnectivity
            ? standard.GetValueOrDefault(connectivity!.Trim()) ?? []
            : standard.Values.SelectMany(values => values);

        var saved = models
            .Where(model => !hasConnectivity || string.Equals(model.Connectivity, connectivity!.Trim(), StringComparison.OrdinalIgnoreCase))
            .SelectMany(used);

        return MergeDistinct(defaults, saved);
    }

    private static IReadOnlyList<string> MergeDistinct(IEnumerable<string> first, IEnumerable<string?> then)
    {
        return first
            .Concat(then.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).Order(StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The model side panel, with how many registered devices use the model.</summary>
    private static async Task<IResult> GetDeviceModelAsync(
        string modelNumber,
        IDeviceModelRepository repository,
        IIoTDeviceRepository devices,
        CancellationToken cancellationToken)
    {
        var model = await repository.GetByModelNumberAsync(modelNumber.Trim(), cancellationToken);
        return model is null
            ? NotFound(modelNumber)
            : Results.Ok(DeviceModelResponse.FromEntity(model, await DeviceCountAsync(devices, model, cancellationToken)));
    }

    /// <summary>The Add Device Model form; SupportedMeasurements, FrequencyBands and ConfigurableAttributes are arrays.</summary>
    private static async Task<IResult> CreateDeviceModelAsync(
        CreateDeviceModelRequest request,
        IDeviceModelRepository repository,
        IDeviceTypeRepository types,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var deviceType = await types.GetByNameAsync(request.DeviceType!.Trim(), cancellationToken);
        if (deviceType is null)
        {
            return UnknownType(request.DeviceType);
        }

        var model = request.ToEntity(currentUserAccessor.GetCurrentUser(), deviceType);

        if (await repository.ModelNumberExistsAsync(model.ModelNumber, cancellationToken))
        {
            return Results.Conflict(new { message = $"Device model '{model.ModelNumber}' is already registered." });
        }

        await repository.CreateAsync(model, cancellationToken);
        await eventLogger.LogAsync(EntityType, model.ModelNumber, $"{model.Manufacturer} {model.ModelNumber}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/device-models/{model.ModelNumber}", DeviceModelResponse.FromEntity(model));
    }

    /// <summary>Takes the same body as create; its ModelNumber must match the one in the URL.</summary>
    private static async Task<IResult> UpdateDeviceModelAsync(
        string modelNumber,
        CreateDeviceModelRequest request,
        IDeviceModelRepository repository,
        IDeviceTypeRepository types,
        IIoTDeviceRepository devices,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (TrackingRecords.KeyChanged("ModelNumber", modelNumber, request.ModelNumber) is { } keyChanged)
        {
            return keyChanged;
        }

        var existing = await repository.GetByModelNumberAsync(modelNumber.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(modelNumber);
        }

        var deviceType = await types.GetByNameAsync(request.DeviceType!.Trim(), cancellationToken);
        if (deviceType is null)
        {
            return UnknownType(request.DeviceType);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var model = request.ToEntity(currentUser, deviceType).Replacing(existing, currentUser);
        model.ModelNumber = existing.ModelNumber;

        if (!await repository.UpdateAsync(existing.Id, model, cancellationToken))
        {
            return NotFound(modelNumber);
        }

        await eventLogger.LogAsync(EntityType, model.ModelNumber, $"{model.Manufacturer} {model.ModelNumber}", EventAction.Updated, cancellationToken);

        return Results.Ok(DeviceModelResponse.FromEntity(model, await DeviceCountAsync(devices, model, cancellationToken)));
    }

    private static async Task<int> DeviceCountAsync(IIoTDeviceRepository devices, DeviceModel model, CancellationToken cancellationToken)
    {
        return TrackingRecords.Listed(await devices.GetAllAsync(cancellationToken)).Count(device => Same(device.Model, model.ModelNumber));
    }

    private static IResult UnknownType(string deviceType)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["DeviceType"] = [$"Device type '{deviceType.Trim()}' does not exist; add it first or pick one from GET /api/device-models/device-types"]
        });
    }

    private static IResult TypeNotFound(string name)
    {
        return Results.NotFound(new { message = $"Device type '{name}' was not found." });
    }

    private static async Task<IResult> DeleteDeviceModelAsync(
        string modelNumber,
        IDeviceModelRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var model = await repository.GetByModelNumberAsync(modelNumber.Trim(), cancellationToken);
        if (model is null || !await repository.DeleteAsync(model.Id, cancellationToken))
        {
            return NotFound(modelNumber);
        }

        await eventLogger.LogAsync(EntityType, model.ModelNumber, $"{model.Manufacturer} {model.ModelNumber}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string modelNumber)
    {
        return Results.NotFound(new { message = $"Device model '{modelNumber}' was not found." });
    }
}
