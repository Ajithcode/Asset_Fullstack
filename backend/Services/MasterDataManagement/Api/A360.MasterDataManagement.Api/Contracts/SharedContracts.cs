using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Api.Contracts;

public static class MasterDataStatuses
{
    public const string Active = "Active";
}

public sealed record MasterDataReferenceDto(
    string? Id,
    string? Name)
{
    public MasterDataReference ToReference()
    {
        return new MasterDataReference
        {
            Id = string.IsNullOrWhiteSpace(Id) ? null : Id.Trim(),
            Name = Name?.Trim() ?? string.Empty
        };
    }

    public static MasterDataReferenceDto FromReference(MasterDataReference reference)
    {
        return new MasterDataReferenceDto(reference.Id, reference.Name);
    }
}

public sealed record MasterDataStepDto(
    string? Name,
    string? Description,
    int? DurationMinutes = null,
    string? ResponseType = null,
    bool? IsRequired = null)
{
    public MasterDataStep ToStep()
    {
        return new MasterDataStep
        {
            Name = Name!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            DurationMinutes = DurationMinutes,
            ResponseType = string.IsNullOrWhiteSpace(ResponseType) ? null : ResponseType.Trim(),
            IsRequired = IsRequired ?? false
        };
    }

    public static MasterDataStepDto FromStep(MasterDataStep step)
    {
        return new MasterDataStepDto(step.Name, step.Description, step.DurationMinutes, step.ResponseType, step.IsRequired);
    }
}
