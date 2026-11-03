
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class WorkOrderValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateWorkOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.WorkOrderId))
        {
            errors["WorkOrderId"] =
                ["WorkOrderId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.WorkType))
        {
            errors["WorkType"] =
                ["WorkType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["Title"] =
                ["Title is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Priority))
        {
            errors["Priority"] =
                ["Priority is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWorkOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.WorkType))
        {
            errors["WorkType"] =
                ["WorkType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["Title"] =
                ["Title is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Priority))
        {
            errors["Priority"] =
                ["Priority is required"];
        }

        return errors;
    }
}
