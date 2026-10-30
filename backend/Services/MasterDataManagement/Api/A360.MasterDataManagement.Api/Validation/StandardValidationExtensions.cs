using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class StandardValidationExtensions
{
    // "http(s)://host.domain/..." as the form checks it.
    private static readonly Regex WebUrlPattern = new(
        @"^https?://[^\s.]+\.\S+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static Dictionary<string, string[]> Validate(
        this CreateStandardRequest request)
    {
        return ValidateFields(request.StandardName, request.Code, request.Organization, request.Category, request.Url, request.PublishedOn, request.NextReview);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStandardRequest request)
    {
        return ValidateFields(request.StandardName, request.Code, request.Organization, request.Category, request.Url, request.PublishedOn, request.NextReview);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStandardStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? standardName,
        string? code,
        string? organization,
        string? category,
        string? url,
        DateOnly? publishedOn,
        DateOnly? nextReview)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(standardName))
        {
            errors["StandardName"] =
                ["StandardName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(organization))
        {
            errors["Organization"] =
                ["Organization is required"];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["Category"] =
                ["Category is required"];
        }

        if (!string.IsNullOrWhiteSpace(url) && !WebUrlPattern.IsMatch(url.Trim()))
        {
            errors["Url"] =
                ["Url must be a full web address such as https://www.iso.org"];
        }

        if (publishedOn is not null && nextReview is not null && nextReview <= publishedOn)
        {
            errors["NextReview"] =
                ["NextReview must be after PublishedOn"];
        }

        return errors;
    }
}
