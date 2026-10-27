using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetCriticalityValidationExtensions
{
    private const int NameMaxLength = 200;
    private const int JustificationMaxLength = 500;
    private const int RemarksMaxLength = 1000;
    private const int MaxCriteria = 20;
    private const decimal TotalWeight = 100;

    public static Dictionary<string, string[]> Validate(this CreateAssetCriticalityRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetRegistrationId))
        {
            errors["AssetRegistrationId"] = ["AssetRegistrationId is required"];
        }

        if (request.AssessmentDate is null)
        {
            errors["AssessmentDate"] = ["AssessmentDate is required"];
        }
        else
        {
            if (request.ValidUntil is not null && request.ValidUntil.Value.Date < request.AssessmentDate.Value.Date)
            {
                errors["ValidUntil"] = ["ValidUntil cannot be before AssessmentDate"];
            }

            if (request.NextReviewDate is not null && request.NextReviewDate.Value.Date < request.AssessmentDate.Value.Date)
            {
                errors["NextReviewDate"] = ["NextReviewDate cannot be before AssessmentDate"];
            }
        }

        RequireText(errors, "AssessedBy", request.AssessedBy, NameMaxLength);
        RequireText(errors, "ApprovedBy", request.ApprovedBy, NameMaxLength);
        ValidateRating(errors, "Likelihood", request.Likelihood);
        ValidateRating(errors, "Consequence", request.Consequence);
        LimitText(errors, "Remarks", request.Remarks, RemarksMaxLength);

        var criteria = request.Criteria ?? [];
        if (criteria.Count == 0)
        {
            errors["Criteria"] = ["Score at least one criterion"];
            return errors;
        }

        if (criteria.Count > MaxCriteria)
        {
            errors["Criteria"] = [$"At most {MaxCriteria} criteria are allowed"];
            return errors;
        }

        var index = 0;
        foreach (var criterion in criteria)
        {
            var prefix = $"Criteria[{index}]";
            RequireText(errors, $"{prefix}.Name", criterion.Name, NameMaxLength);
            RequireText(errors, $"{prefix}.Justification", criterion.Justification, JustificationMaxLength);

            if (criterion.Weight is null or <= 0 or > TotalWeight)
            {
                errors[$"{prefix}.Weight"] = [$"Weight must be greater than 0 and at most {TotalWeight}"];
            }

            if (criterion.Score is null
                || criterion.Score < AssetCriticalityCriteria.MinScore
                || criterion.Score > AssetCriticalityCriteria.MaxScore
                || criterion.Score.Value * 2 % 1 != 0)
            {
                errors[$"{prefix}.Score"] = [$"Score must be from {AssetCriticalityCriteria.MinScore} to {AssetCriticalityCriteria.MaxScore} in steps of 0.5"];
            }

            index++;
        }

        if (criteria.All(criterion => criterion.Weight is not null) && criteria.Sum(criterion => criterion.Weight!.Value) != TotalWeight)
        {
            errors["Criteria"] = [$"Criteria weights must add up to {TotalWeight}"];
        }

        var duplicateNames = criteria
            .Where(criterion => !string.IsNullOrWhiteSpace(criterion.Name))
            .GroupBy(criterion => criterion.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            errors["Criteria"] = [$"Each criterion can be sent only once: {string.Join(", ", duplicateNames)}"];
        }

        return errors;
    }

    private static void ValidateRating(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !AssetRiskRatings.IsValid(value))
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", AssetRiskRatings.All)}"];
        }
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else
        {
            LimitText(errors, field, value, maxLength);
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
