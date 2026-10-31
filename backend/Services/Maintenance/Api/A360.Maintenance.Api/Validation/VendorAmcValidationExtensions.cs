
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class VendorAmcValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateVendorAmcRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.VendorName))
        {
            errors["VendorName"] =
                ["VendorName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ContractId))
        {
            errors["ContractId"] =
                ["ContractId is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateVendorAmcRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.VendorName))
        {
            errors["VendorName"] =
                ["VendorName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ContractId))
        {
            errors["ContractId"] =
                ["ContractId is required"];
        }

        return errors;
    }
}
