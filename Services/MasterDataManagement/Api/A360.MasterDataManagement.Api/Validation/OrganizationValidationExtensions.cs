using A360.MasterDataManagement.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Validation;

public static class OrganizationValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateOrganizationRequest request)
    {
        return ValidateFields(
            request.OrganizationName,
            request.ParentOrganization,
            request.BusinessUnits,
            request.Sites);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateOrganizationRequest request)
    {
        return ValidateFields(
            request.OrganizationName,
            request.ParentOrganization,
            request.BusinessUnits,
            request.Sites);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateOrganizationStatusRequest request)
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
        string? organizationName,
        MasterDataReferenceDto? parentOrganization,
        List<MasterDataReferenceDto>? businessUnits,
        List<MasterDataReferenceDto>? sites)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(organizationName))
        {
            errors["OrganizationName"] =
                ["OrganizationName is required"];
        }

        // The parent is another organization record, so it must be picked by id.
        if (parentOrganization is not null && !MongoObjectId.IsValid(parentOrganization.Id ?? string.Empty))
        {
            errors["ParentOrganization"] =
                ["ParentOrganization must have a valid organization id"];
        }

        MasterDataReferenceValidation.ValidateList(errors, "BusinessUnits", businessUnits, idResolvesName: true);
        MasterDataReferenceValidation.ValidateList(errors, "Sites", sites, idResolvesName: true);

        return errors;
    }
}
