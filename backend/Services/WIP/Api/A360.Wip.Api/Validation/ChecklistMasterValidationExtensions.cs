
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class ChecklistMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateChecklistMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ChecklistId))
        {
            errors["ChecklistId"] =
                ["ChecklistId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ChecklistName))
        {
            errors["ChecklistName"] =
                ["ChecklistName is required"];
        }

        if (request.VersionNumber is < 1)
        {
            errors["VersionNumber"] =
                ["VersionNumber must be greater than 0"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateChecklistMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ChecklistName))
        {
            errors["ChecklistName"] =
                ["ChecklistName is required"];
        }

        if (request.VersionNumber is < 1)
        {
            errors["VersionNumber"] =
                ["VersionNumber must be greater than 0"];
        }

        return errors;
    }
}
