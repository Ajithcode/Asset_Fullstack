using A360.MasterDataManagement.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Validation;

public static class OrganizationValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateOrganizationRequest request)
    {
        var errors = ValidateFields(
            request.OrganizationName,
            request.ParentOrganization,
            request.BusinessUnits,
            request.Sites);

        ValidateDetails(errors, request.Code, request.Website, request.Contact, request.Settings);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateOrganizationRequest request)
    {
        var errors = ValidateFields(
            request.OrganizationName,
            request.ParentOrganization,
            request.BusinessUnits,
            request.Sites);

        ValidateDetails(errors, request.Code, request.Website, request.Contact, request.Settings);

        return errors;
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

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        string? code,
        string? website,
        StructureContactDto? contact,
        OrganizationSettingsDto? settings)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        StructureValidation.ValidateWebsite(errors, website);
        StructureValidation.ValidateEmail(errors, "Email", contact?.Email);
        StructureValidation.ValidateEffectiveDates(errors, settings?.EffectiveFrom, settings?.EffectiveTo);
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
