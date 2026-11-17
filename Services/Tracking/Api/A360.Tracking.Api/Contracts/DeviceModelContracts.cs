using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

// ---------------------------------------------------------------- Device models

/// <summary>
/// The Add Device Model form: model, manufacturer, device type, connectivity, protocol, power, frequency bands,
/// supported measurements, IP rating, battery life, operating temperature, firmware range, status and configurable attributes.
/// </summary>
/// <param name="ModelNumber">The "Model name" field, e.g. EM300-TH.</param>
/// <param name="DeviceType">Name of a device type from GET /api/device-models/device-types.</param>
/// <param name="Connectivity">One of <see cref="DeviceModelConnectivity.All"/>.</param>
/// <param name="SupportedMeasurements">Array of measurement names from <see cref="DeviceMeasurement.All"/>, e.g. ["Temperature","Humidity"].</param>
/// <param name="PowerSource">One of <see cref="DeviceModelPowerSource.All"/>.</param>
/// <param name="FrequencyBands">Array, e.g. ["EU868","US915"].</param>
/// <param name="ConfigurableAttributes">Array of names from <see cref="DeviceModelAttribute.All"/>, e.g. ["Reporting Interval"].</param>
/// <param name="Status">One of <see cref="DeviceModelStatus.All"/>; defaults to Draft.</param>
public sealed record CreateDeviceModelRequest(
    string? ModelNumber,
    string? Manufacturer,
    string? DeviceType,
    string? Connectivity,
    IReadOnlyList<string>? SupportedMeasurements,
    string? PowerSource,
    string? Protocol,
    IReadOnlyList<string>? FrequencyBands,
    string? IpRating,
    string? BatteryLife,
    string? OperatingTemperature,
    string? FirmwareFrom,
    string? FirmwareTo,
    IReadOnlyList<string>? ConfigurableAttributes,
    string? Status)
{
    /// <param name="deviceType">The stored device type, so the model keeps its exact name.</param>
    public DeviceModel ToEntity(CurrentUser? currentUser, DeviceType deviceType)
    {
        return new DeviceModel
        {
            ModelNumber = ModelNumber!.Trim(),
            Manufacturer = Manufacturer!.Trim(),
            DeviceType = deviceType.Name,
            Connectivity = ContractText.Canonical(Connectivity, DeviceModelConnectivity.All)!,
            SupportedMeasurements = Canonical(SupportedMeasurements, DeviceMeasurement.All.Select(m => m.Name)),
            PowerSource = ContractText.Canonical(PowerSource, DeviceModelPowerSource.All)!,
            Protocol = ContractText.TrimOrNull(Protocol),
            FrequencyBands = DistinctTexts(FrequencyBands),
            IpRating = ContractText.TrimOrNull(IpRating),
            BatteryLife = ContractText.TrimOrNull(BatteryLife),
            OperatingTemperature = ContractText.TrimOrNull(OperatingTemperature),
            FirmwareFrom = ContractText.TrimOrNull(FirmwareFrom),
            FirmwareTo = ContractText.TrimOrNull(FirmwareTo),
            ConfigurableAttributes = Canonical(ConfigurableAttributes, DeviceModelAttribute.All),
            Status = ContractText.Canonical(Status, DeviceModelStatus.All) ?? DeviceModelStatus.Draft,
            CreatedBy = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            IsDeleted = false
        };
    }

    private static List<string> DistinctTexts(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Each value in its listed spelling (e.g. "co₂" → "CO₂"); validation has already rejected unknown ones.</summary>
    private static List<string> Canonical(IReadOnlyList<string>? values, IEnumerable<string> allowed)
    {
        var options = allowed.ToList();
        return DistinctTexts(values).Select(value => ContractText.Canonical(value, options) ?? value).ToList();
    }
}

/// <summary>Query string of the model table.</summary>
public sealed class DeviceModelListQuery
{
    /// <summary>Matches model number, manufacturer, device type or protocol.</summary>
    public string? Search { get; set; }
    public string? DeviceType { get; set; }
    public string? Manufacturer { get; set; }
    public string? Connectivity { get; set; }
    public string? Status { get; set; }

    /// <summary>1-based; default 1.</summary>
    public int? Page { get; set; }

    /// <summary>1-200; default 8.</summary>
    public int? PageSize { get; set; }
}

/// <summary>A row of the model table.</summary>
public sealed record DeviceModelListItemResponse(
    string ModelNumber,
    string Manufacturer,
    string DeviceType,
    string Connectivity,
    IReadOnlyList<string> SupportedMeasurements,
    string PowerSource,
    string? Protocol,
    string? Status)
{
    public static DeviceModelListItemResponse FromEntity(DeviceModel model) => new(
        model.ModelNumber,
        model.Manufacturer,
        model.DeviceType,
        model.Connectivity,
        model.SupportedMeasurements,
        model.PowerSource,
        model.Protocol,
        model.Status);
}

/// <summary>The model side panel. <see cref="Measurements"/> carries each supported measurement with its unit.</summary>
public sealed record DeviceModelResponse(
    string Id,
    string ModelNumber,
    string Manufacturer,
    string DeviceType,
    string Connectivity,
    IReadOnlyList<string> SupportedMeasurements,
    IReadOnlyList<DeviceMeasurement> Measurements,
    string PowerSource,
    string? Protocol,
    IReadOnlyList<string> FrequencyBands,
    string? IpRating,
    string? BatteryLife,
    string? OperatingTemperature,
    string? FirmwareFrom,
    string? FirmwareTo,
    IReadOnlyList<string> ConfigurableAttributes,
    string? Status,
    int DeviceCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public static DeviceModelResponse FromEntity(DeviceModel model, int deviceCount = 0)
    {
        return new DeviceModelResponse(
            model.Id,
            model.ModelNumber,
            model.Manufacturer,
            model.DeviceType,
            model.Connectivity,
            model.SupportedMeasurements,
            model.SupportedMeasurements
                .Select(name => DeviceMeasurement.All.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? new DeviceMeasurement(name, string.Empty))
                .ToList(),
            model.PowerSource,
            model.Protocol,
            model.FrequencyBands,
            model.IpRating,
            model.BatteryLife,
            model.OperatingTemperature,
            model.FirmwareFrom,
            model.FirmwareTo,
            model.ConfigurableAttributes,
            model.Status,
            deviceCount,
            model.CreatedBy,
            model.CreatedAt,
            model.UpdatedBy,
            model.UpdatedAt);
    }
}

/// <summary>The four cards: Device Types, Models, Active Models and Pending Review (Draft or Pending Review).</summary>
public sealed record DeviceModelSummaryResponse(int DeviceTypes, int Models, int ActiveModels, int PendingReview);

// ---------------------------------------------------------------- Device types

/// <summary>The Add Device Type form.</summary>
/// <param name="Icon">One of <see cref="DeviceTypeIcon.All"/>; defaults to chip.</param>
/// <param name="ThumbnailStyle">One of <see cref="DeviceTypeThumbnail.All"/>; defaults to Sensor box.</param>
public sealed record CreateDeviceTypeRequest(string? Name, string? Icon, string? ThumbnailStyle, string? Description);

/// <summary>A Device Type list entry, with how many models it has.</summary>
public sealed record DeviceTypeResponse(
    string Id,
    string Name,
    string Icon,
    string ThumbnailStyle,
    string? Description,
    int ModelCount,
    int ActiveModelCount)
{
    public static DeviceTypeResponse FromEntity(DeviceType type, IReadOnlyCollection<DeviceModel> models) => new(
        type.Id,
        type.Name,
        type.Icon,
        type.ThumbnailStyle,
        type.Description,
        models.Count,
        models.Count(model => model.Status == DeviceModelStatus.Active));
}

/// <summary>
/// One device type with the Model Capability Overview: the connectivities, measurements and configurable
/// attributes its models support, each with how many models support it.
/// </summary>
public sealed record DeviceTypeDetailsResponse(
    DeviceTypeResponse DeviceType,
    IReadOnlyList<CapabilityCountResponse> Connectivities,
    IReadOnlyList<CapabilityCountResponse> Measurements,
    IReadOnlyList<CapabilityCountResponse> ConfigurableAttributes);

/// <summary>A capability and how many of the type's models have it; Unit is set for measurements.</summary>
public sealed record CapabilityCountResponse(string Name, string? Unit, int ModelCount);

/// <summary>Every dropdown of the Add Device Model and Add Device Type forms in one response.</summary>
public sealed record DeviceModelFormOptionsResponse(
    IReadOnlyList<DeviceTypeResponse> DeviceTypes,
    IReadOnlyList<string> Manufacturers,
    IReadOnlyList<string> Connectivities,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ProtocolsByConnectivity,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FrequencyBandsByConnectivity,
    IReadOnlyList<string> PowerSources,
    IReadOnlyList<DeviceMeasurement> Measurements,
    IReadOnlyList<string> IpRatings,
    IReadOnlyList<string> ConfigurableAttributes,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> DeviceTypeIcons,
    IReadOnlyList<string> ThumbnailStyles);
