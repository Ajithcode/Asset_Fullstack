using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SkillValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSkillRequest request)
    {
        return ValidateFields(
            request.SkillName, request.Code, request.Levels, request.CertificationRequired, request.CertificationValidityMonths);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSkillRequest request)
    {
        return ValidateFields(
            request.SkillName, request.Code, request.Levels, request.CertificationRequired, request.CertificationValidityMonths);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSkillStatusRequest request)
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
        string? skillName,
        string? code,
        List<string>? levels,
        bool? certificationRequired,
        int? certificationValidityMonths)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(skillName))
        {
            errors["SkillName"] =
                ["SkillName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (levels is not null)
        {
            if (levels.Any(string.IsNullOrWhiteSpace))
            {
                errors["Levels"] =
                    ["Every level needs a name"];
            }
            else if (levels.GroupBy(x => x.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            {
                errors["Levels"] =
                    ["Level names must be different"];
            }
        }

        if (certificationRequired == true && certificationValidityMonths is not > 0)
        {
            errors["CertificationValidityMonths"] =
                ["CertificationValidityMonths is required when a certification is required"];
        }

        return errors;
    }
}
