using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SiteValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSiteRequest request)
    {
        return ValidateFields(request.SiteName, request.Code, request.BusinessUnit, request.Department);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSiteRequest request)
    {
        return ValidateFields(request.SiteName, request.Code, request.BusinessUnit, request.Department);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSiteStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? siteName,
        string? code,
        MasterDataReferenceDto? businessUnit,
        MasterDataReferenceDto? department)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(siteName))
        {
            errors["SiteName"] =
                ["SiteName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        // Business unit and department are records in this service, so they are picked by id.
        if (businessUnit is not null && string.IsNullOrWhiteSpace(businessUnit.Id))
        {
            errors["BusinessUnit"] =
                ["BusinessUnit must have a business unit id"];
        }

        if (department is not null && string.IsNullOrWhiteSpace(department.Id))
        {
            errors["Department"] =
                ["Department must have a department id"];
        }

        return errors;
    }
}
