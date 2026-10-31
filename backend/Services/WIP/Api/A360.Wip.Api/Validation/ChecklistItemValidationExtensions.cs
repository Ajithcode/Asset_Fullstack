
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class ChecklistItemValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateChecklistItemRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            errors["ItemId"] =
                ["ItemId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ItemDescription))
        {
            errors["ItemDescription"] =
                ["ItemDescription is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ChecklistId))
        {
            errors["ChecklistId"] =
                ["ChecklistId is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateChecklistItemRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ItemDescription))
        {
            errors["ItemDescription"] =
                ["ItemDescription is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ChecklistId))
        {
            errors["ChecklistId"] =
                ["ChecklistId is required"];
        }

        return errors;
    }
}
