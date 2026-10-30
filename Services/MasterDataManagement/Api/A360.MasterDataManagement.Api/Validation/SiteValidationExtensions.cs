using A360.MasterDataManagement.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Validation;

public static class SiteValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSiteRequest request)
    {
        var errors = ValidateFields(request.SiteName, request.Code, request.BusinessUnit, request.Department);

        ValidateDetails(errors, request.Organization, request.ParentSite, request.Latitude, request.Longitude, request.Manager, request.Emergency);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSiteRequest request)
    {
        var errors = ValidateFields(request.SiteName, request.Code, request.BusinessUnit, request.Department);

        ValidateDetails(errors, request.Organization, request.ParentSite, request.Latitude, request.Longitude, request.Manager, request.Emergency);

        return errors;
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

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        MasterDataReferenceDto? organization,
        MasterDataReferenceDto? parentSite,
        double? latitude,
        double? longitude,
        StructureContactDto? manager,
        StructureContactDto? emergency)
    {
        // The organization and parent site are records in this service, so they are picked by id.
        if (organization is not null && !MongoObjectId.IsValid(organization.Id ?? string.Empty))
        {
            errors["Organization"] =
                ["Organization must have a valid organization id"];
        }

        StructureValidation.ValidateParent(errors, "ParentSite", parentSite);
        StructureValidation.ValidateCoordinates(errors, latitude, longitude);
        StructureValidation.ValidateEmail(errors, "ManagerEmail", manager?.Email);
        StructureValidation.ValidateEmail(errors, "EmergencyEmail", emergency?.Email);
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
