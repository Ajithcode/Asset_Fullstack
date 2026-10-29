using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class BusinessUnitValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateBusinessUnitRequest request)
    {
        return ValidateFields(
            request.BusinessUnitName,
            request.Code,
            request.ParentOrganization,
            request.Departments,
            request.Sites);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBusinessUnitRequest request)
    {
        return ValidateFields(
            request.BusinessUnitName,
            request.Code,
            request.ParentOrganization,
            request.Departments,
            request.Sites);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBusinessUnitStatusRequest request)
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
        string? businessUnitName,
        string? code,
        MasterDataReferenceDto? parentOrganization,
        List<MasterDataReferenceDto>? departments,
        List<MasterDataReferenceDto>? sites)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(businessUnitName))
        {
            errors["BusinessUnitName"] =
                ["BusinessUnitName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        // With an id the name is filled in from the organization master; without one a name is needed.
        if (parentOrganization is not null
            && string.IsNullOrWhiteSpace(parentOrganization.Id)
            && string.IsNullOrWhiteSpace(parentOrganization.Name))
        {
            errors["ParentOrganization"] =
                ["ParentOrganization needs an id or a name"];
        }

        MasterDataReferenceValidation.ValidateList(errors, "Departments", departments, idResolvesName: true);
        MasterDataReferenceValidation.ValidateList(errors, "Sites", sites, idResolvesName: true);

        return errors;
    }
}
