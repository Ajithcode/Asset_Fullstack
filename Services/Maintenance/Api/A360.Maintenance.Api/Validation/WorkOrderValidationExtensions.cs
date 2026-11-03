
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

        if (request.Progress is < 0 or > 100)
        {
            errors["Progress"] = ["Progress must be between 0 and 100"];
        }

        if (request.Parts?.Any(part => string.IsNullOrWhiteSpace(part.PartName) || part.Quantity < 0 || part.UnitCost < 0) == true)
        {
            errors["Parts"] = ["Each part needs a name and a non-negative quantity and unit cost"];
        }

        if (request.Labour?.Any(labour => string.IsNullOrWhiteSpace(labour.Technician) || labour.Hours < 0 || labour.HourlyRate < 0) == true)
        {
            errors["Labour"] = ["Each labour entry needs a technician and non-negative hours and rate"];
        }

        if (request.Tasks?.Any(task => string.IsNullOrWhiteSpace(task.Title)) == true)
        {
            errors["Tasks"] = ["Each task needs a title"];
        }

        if (request.PlannedStartDate is not null && request.PlannedEndDate is not null && request.PlannedEndDate < request.PlannedStartDate)
        {
            errors["PlannedEndDate"] = ["Planned end cannot be before planned start"];
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

        if (request.Progress is < 0 or > 100)
        {
            errors["Progress"] = ["Progress must be between 0 and 100"];
        }

        if (request.Parts?.Any(part => string.IsNullOrWhiteSpace(part.PartName) || part.Quantity < 0 || part.UnitCost < 0) == true)
        {
            errors["Parts"] = ["Each part needs a name and a non-negative quantity and unit cost"];
        }

        if (request.Labour?.Any(labour => string.IsNullOrWhiteSpace(labour.Technician) || labour.Hours < 0 || labour.HourlyRate < 0) == true)
        {
            errors["Labour"] = ["Each labour entry needs a technician and non-negative hours and rate"];
        }

        if (request.Tasks?.Any(task => string.IsNullOrWhiteSpace(task.Title)) == true)
        {
            errors["Tasks"] = ["Each task needs a title"];
        }

        if (request.PlannedStartDate is not null && request.PlannedEndDate is not null && request.PlannedEndDate < request.PlannedStartDate)
        {
            errors["PlannedEndDate"] = ["Planned end cannot be before planned start"];
        }

        return errors;
    }
}
