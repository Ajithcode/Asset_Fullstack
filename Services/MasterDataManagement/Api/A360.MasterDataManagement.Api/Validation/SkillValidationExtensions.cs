using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SkillValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSkillRequest request)
    {
        return ValidateFields(
            request.SkillName, request.Code);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSkillRequest request)
    {
        return ValidateFields(
            request.SkillName, request.Code);
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
        string? code)
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

        return errors;
    }
}
