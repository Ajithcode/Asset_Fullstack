using A360.Repository.Repositories;
using System.Net.Mail;

namespace A360.UserAccount.Api.Validation;

internal sealed class ValidationErrorBuilder
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public int Count => _errors.Count;

    public void Required(string fieldName, string? value, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(fieldName, $"{displayName} is required.");
        }
    }

    public void Email(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(fieldName, "Email is required.");
            return;
        }

        try
        {
            var address = new MailAddress(value);
            if (!string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Add(fieldName, "Email must be a valid email address.");
            }
        }
        catch (FormatException)
        {
            Add(fieldName, "Email must be a valid email address.");
        }
    }

    public void ObjectId(string fieldName, string? value, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(fieldName, $"{displayName} is required.");
            return;
        }

        if (!MongoObjectId.IsValid(value))
        {
            Add(fieldName, $"{displayName} must be a valid MongoDB ObjectId.");
        }
    }

    public void Error(string fieldName, string message)
    {
        Add(fieldName, message);
    }

    public IDictionary<string, string[]> ToDictionary()
    {
        return _errors.ToDictionary(error => error.Key, error => error.Value.ToArray());
    }

    private void Add(string fieldName, string message)
    {
        if (!_errors.TryGetValue(fieldName, out var messages))
        {
            messages = [];
            _errors[fieldName] = messages;
        }

        messages.Add(message);
    }
}
