
using A360.Workflows.Api.Contracts;

namespace A360.Workflows.Api.Validation;

public static class WorkflowListValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateWorkflowListRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.WorkflowName))
        {
            errors["WorkflowName"] =
                ["WorkflowName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Module))
        {
            errors["Module"] =
                ["Module is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWorkflowListRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.WorkflowName))
        {
            errors["WorkflowName"] =
                ["WorkflowName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Module))
        {
            errors["Module"] =
                ["Module is required"];
        }

        return errors;
    }
}
