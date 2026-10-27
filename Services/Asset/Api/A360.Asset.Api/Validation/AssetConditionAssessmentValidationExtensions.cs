using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetConditionAssessmentValidationExtensions
{
    private const int AssessedByMaxLength = 200;
    private const int RemarksMaxLength = 1000;

    public static Dictionary<string, string[]> Validate(this CreateAssetConditionAssessmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetRegistrationId))
        {
            errors["AssetRegistrationId"] = ["AssetRegistrationId is required"];
        }

        // Assessed By falls back to the signed-in user, so only its length is checked here.
        LimitText(errors, "AssessedBy", request.AssessedBy, AssessedByMaxLength);

        ValidateDetails(errors, request.AssessmentDate, request.OverallCondition, request.HealthScore,
            request.PhysicalCondition, request.Performance, request.Safety, request.Remarks);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateAssetConditionAssessmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssessedBy))
        {
            errors["AssessedBy"] = ["AssessedBy is required"];
        }
        else
        {
            LimitText(errors, "AssessedBy", request.AssessedBy, AssessedByMaxLength);
        }

        ValidateDetails(errors, request.AssessmentDate, request.OverallCondition, request.HealthScore,
            request.PhysicalCondition, request.Performance, request.Safety, request.Remarks);

        return errors;
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        DateTime? assessmentDate,
        string? overallCondition,
        decimal? healthScore,
        string? physicalCondition,
        string? performance,
        string? safety,
        string? remarks)
    {
        if (assessmentDate is null)
        {
            errors["AssessmentDate"] = ["AssessmentDate is required"];
        }
        else if (assessmentDate.Value.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            // One day of slack so a user ahead of UTC can record today's assessment.
            errors["AssessmentDate"] = ["AssessmentDate cannot be in the future"];
        }

        if (string.IsNullOrWhiteSpace(overallCondition))
        {
            errors["OverallCondition"] = ["OverallCondition is required"];
        }
        else
        {
            ValidateRating(errors, "OverallCondition", overallCondition);
        }

        if (healthScore is null)
        {
            errors["HealthScore"] = ["HealthScore is required"];
        }
        else if (healthScore is < 0 or > 100)
        {
            errors["HealthScore"] = ["HealthScore must be between 0 and 100"];
        }

        ValidateRating(errors, "PhysicalCondition", physicalCondition);
        ValidateRating(errors, "Performance", performance);
        ValidateRating(errors, "Safety", safety);
        LimitText(errors, "Remarks", remarks, RemarksMaxLength);
    }

    private static void ValidateRating(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !AssetConditionRatings.IsValid(value))
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", AssetConditionRatings.All)}"];
        }
    }

    private static void LimitText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
