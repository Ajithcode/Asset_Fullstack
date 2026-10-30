using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// The Add Device Model form: model, manufacturer, device type, connectivity, protocol, power, frequency bands,
/// supported measurements, IP rating, battery life, operating temperature, firmware range, status and configurable attributes.
/// </summary>
/// <param name="ModelNumber">The "Model name" field, e.g. EM300-TH.</param>
/// <param name="Connectivity">One of <see cref="DeviceModelConnectivity.All"/>.</param>
/// <param name="PowerSource">One of <see cref="DeviceModelPowerSource.All"/>.</param>
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
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public DeviceModel ToEntity(CurrentUser? currentUser)
    {
        return new DeviceModel
        {
            ModelNumber = ModelNumber!.Trim(),
            Manufacturer = Manufacturer!.Trim(),
            DeviceType = DeviceType!.Trim(),
            Connectivity = ContractText.Canonical(Connectivity, DeviceModelConnectivity.All)!,
            SupportedMeasurements = DistinctTexts(SupportedMeasurements),
            PowerSource = ContractText.Canonical(PowerSource, DeviceModelPowerSource.All)!,
            Protocol = ContractText.TrimOrNull(Protocol),
            FrequencyBands = DistinctTexts(FrequencyBands),
            IpRating = ContractText.TrimOrNull(IpRating),
            BatteryLife = ContractText.TrimOrNull(BatteryLife),
            OperatingTemperature = ContractText.TrimOrNull(OperatingTemperature),
            FirmwareFrom = ContractText.TrimOrNull(FirmwareFrom),
            FirmwareTo = ContractText.TrimOrNull(FirmwareTo),
            ConfigurableAttributes = DistinctTexts(ConfigurableAttributes),
            Status = ContractText.Canonical(Status, DeviceModelStatus.All) ?? DeviceModelStatus.Draft,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
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
}

public sealed record DeviceModelResponse(
    string Id,
    string ModelNumber,
    string Manufacturer,
    string DeviceType,
    string Connectivity,
    IReadOnlyList<string> SupportedMeasurements,
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
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static DeviceModelResponse FromEntity(DeviceModel model)
    {
        return new DeviceModelResponse(
            model.Id,
            model.ModelNumber,
            model.Manufacturer,
            model.DeviceType,
            model.Connectivity,
            model.SupportedMeasurements,
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
            model.CreatedBy,
            model.CreatedAt,
            model.UpdatedBy,
            model.UpdatedAt,
            model.ClientId,
            model.TenantId);
    }
}

/// <summary>A Device Type list entry, with how many models it has.</summary>
public sealed record DeviceTypeOptionResponse(string DeviceType, int ModelCount);
