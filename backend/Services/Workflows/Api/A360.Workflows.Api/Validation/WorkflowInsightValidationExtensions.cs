
using A360.Workflows.Api.Contracts;

namespace A360.Workflows.Api.Validation;

public static class WorkflowInsightValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateWorkflowInsightRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.WorkflowName))
        {
            errors["WorkflowName"] =
                ["WorkflowName is required"];
        }

        if (request.PendingApprovals < 0)
        {
            errors["PendingApprovals"] =
                ["PendingApprovals cannot be negative"];
        }

        if (request.TotalInstances < 0)
        {
            errors["TotalInstances"] =
                ["TotalInstances cannot be negative"];
        }

        if (request.AvgApprovalTimeHrs < 0)
        {
            errors["AvgApprovalTimeHrs"] =
                ["AvgApprovalTimeHrs cannot be negative"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWorkflowInsightRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.WorkflowName))
        {
            errors["WorkflowName"] =
                ["WorkflowName is required"];
        }

        if (request.PendingApprovals < 0)
        {
            errors["PendingApprovals"] =
                ["PendingApprovals cannot be negative"];
        }

        if (request.TotalInstances < 0)
        {
            errors["TotalInstances"] =
                ["TotalInstances cannot be negative"];
        }

        if (request.AvgApprovalTimeHrs < 0)
        {
            errors["AvgApprovalTimeHrs"] =
                ["AvgApprovalTimeHrs cannot be negative"];
        }

        return errors;
    }
}
