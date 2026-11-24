
using A360.Workflows.Api.Contracts;

namespace A360.Workflows.Api.Validation;

public static class WorkflowBuilderValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateWorkflowBuilderRequest request)
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

        ValidateDesign(request.Design, errors);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWorkflowBuilderRequest request)
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

        ValidateDesign(request.Design, errors);

        return errors;
    }

    // The design is stored as-is, so it must at least be JSON and of a sensible size.
    private const int MaxDesignLength = 1_000_000;

    private static void ValidateDesign(string? design, Dictionary<string, string[]> errors)
    {
        if (design is null)
        {
            return;
        }

        if (design.Length > MaxDesignLength)
        {
            errors["Design"] = [$"Design must be at most {MaxDesignLength} characters"];
            return;
        }

        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(design);
        }
        catch (System.Text.Json.JsonException)
        {
            errors["Design"] = ["Design must be valid JSON"];
        }
    }
}
