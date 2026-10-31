
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class PermitMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreatePermitMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PermitId))
        {
            errors["PermitId"] =
                ["PermitId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.PermitType))
        {
            errors["PermitType"] =
                ["PermitType is required"];
        }

        if (request.ValidFrom is not null &&
            request.ValidTo is not null &&
            request.ValidTo < request.ValidFrom)
        {
            errors["ValidTo"] =
                ["ValidTo must not be earlier than ValidFrom"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePermitMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PermitType))
        {
            errors["PermitType"] =
                ["PermitType is required"];
        }

        if (request.ValidFrom is not null &&
            request.ValidTo is not null &&
            request.ValidTo < request.ValidFrom)
        {
            errors["ValidTo"] =
                ["ValidTo must not be earlier than ValidFrom"];
        }

        return errors;
    }
}
