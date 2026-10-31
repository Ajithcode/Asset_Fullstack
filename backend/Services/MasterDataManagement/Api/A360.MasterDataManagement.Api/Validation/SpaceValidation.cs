using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

/// <summary>
/// Field checks shared by buildings, floors and zones.
/// </summary>
internal static class SpaceValidation
{
    public static Dictionary<string, string[]> ValidateCommonFields(
        string? name,
        string? code,
        MasterDataReferenceDto? site,
        int? capacity,
        double? area)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["Name"] =
                ["Name is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        // Sites are records in this service, so they are picked by id.
        if (site is not null && string.IsNullOrWhiteSpace(site.Id))
        {
            errors["Site"] =
                ["Site must have a site id"];
        }

        if (capacity < 0)
        {
            errors["Capacity"] =
                ["Capacity cannot be negative"];
        }

        if (area < 0)
        {
            errors["Area"] =
                ["Area cannot be negative"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateStatus(
        string? status)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }
}
