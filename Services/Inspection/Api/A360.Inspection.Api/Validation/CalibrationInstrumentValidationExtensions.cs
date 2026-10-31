using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class CalibrationInstrumentValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateCalibrationInstrumentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.InstrumentCode))
        {
            errors["InstrumentCode"] = ["InstrumentCode is required"];
        }

        if (string.IsNullOrWhiteSpace(request.InstrumentName))
        {
            errors["InstrumentName"] = ["InstrumentName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateCalibrationInstrumentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.InstrumentName))
        {
            errors["InstrumentName"] = ["InstrumentName is required"];
        }

        return errors;
    }
}
