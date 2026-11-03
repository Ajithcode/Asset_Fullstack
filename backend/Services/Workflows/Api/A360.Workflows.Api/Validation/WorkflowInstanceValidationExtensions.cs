
using A360.Workflows.Api.Contracts;

namespace A360.Workflows.Api.Validation;

public static class WorkflowInstanceValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateWorkflowInstanceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.WorkflowName))
        {
            errors["WorkflowName"] =
                ["WorkflowName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.RequestedBy))
        {
            errors["RequestedBy"] =
                ["RequestedBy is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWorkflowInstanceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.WorkflowName))
        {
            errors["WorkflowName"] =
                ["WorkflowName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.RequestedBy))
        {
            errors["RequestedBy"] =
                ["RequestedBy is required"];
        }

        return errors;
    }
}
