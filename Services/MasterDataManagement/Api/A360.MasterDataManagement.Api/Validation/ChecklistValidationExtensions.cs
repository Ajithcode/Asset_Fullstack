using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ChecklistValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateChecklistRequest request)
    {
        return ValidateFields(request.ChecklistName, request.Code, request.ChecklistType, request.Category, request.Items);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateChecklistRequest request)
    {
        return ValidateFields(request.ChecklistName, request.Code, request.ChecklistType, request.Category, request.Items);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateChecklistStatusRequest request)
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
        string? checklistName,
        string? code,
        string? checklistType,
        string? category,
        List<MasterDataStepDto>? items)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(checklistName))
        {
            errors["ChecklistName"] =
                ["ChecklistName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(checklistType))
        {
            errors["ChecklistType"] =
                ["ChecklistType is required"];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["Category"] =
                ["Category is required"];
        }

        if (items?.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)) == true)
        {
            errors["Items"] =
                ["Every entry in Items needs a name"];
        }

        return errors;
    }
}
