
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class TaskMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateTaskMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TaskId))
        {
            errors["TaskId"] =
                ["TaskId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.TaskName))
        {
            errors["TaskName"] =
                ["TaskName is required"];
        }

        if (request.CompletionPercentage is < 0 or > 100)
        {
            errors["CompletionPercentage"] =
                ["CompletionPercentage must be between 0 and 100"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTaskMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TaskName))
        {
            errors["TaskName"] =
                ["TaskName is required"];
        }

        if (request.CompletionPercentage is < 0 or > 100)
        {
            errors["CompletionPercentage"] =
                ["CompletionPercentage must be between 0 and 100"];
        }

        return errors;
    }
}
