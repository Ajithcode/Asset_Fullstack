using System.Text.RegularExpressions;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

/// <summary>The Validation column: Valid, or Invalid with the reason.</summary>
public sealed record TagValidationResult(string Result, string? Message)
{
    public static readonly TagValidationResult Valid = new(TagValidation.Valid, null);

    public static TagValidationResult Invalid(string message) => new(TagValidation.Invalid, message);

    public bool IsValid => Result == TagValidation.Valid;
}

/// <summary>The format of a tag's value for each technology: MAC address, EPC, encoded value or UID.</summary>
public static partial class TagValueFormat
{
    /// <summary>Puts the value in its stored form: AC:23:3F:DE:E2:87 for MAC / UID, "3008 33B2 DDD9 0140 0000 0001" for EPC.</summary>
    public static string? Normalize(string technology, string? value)
    {
        var trimmed = ContractText.TrimOrNull(value);
        if (trimmed is null)
        {
            return null;
        }

        switch (technology)
        {
            case TagTechnology.Ble:
            case TagTechnology.Nfc:
            {
                var hex = Separators().Replace(trimmed, string.Empty).ToUpperInvariant();
                return HexOnly().IsMatch(hex) && hex.Length % 2 == 0
                    ? string.Join(':', hex.Chunk(2).Select(pair => new string(pair)))
                    : trimmed.ToUpperInvariant();
            }
            case TagTechnology.PassiveRfid:
            case TagTechnology.ActiveRfid:
            {
                var hex = Separators().Replace(trimmed, string.Empty).ToUpperInvariant();
                return HexOnly().IsMatch(hex)
                    ? string.Join(' ', hex.Chunk(4).Select(group => new string(group)))
                    : trimmed.ToUpperInvariant();
            }
            default:
                return trimmed;
        }
    }

    /// <summary>Null when the (normalized) value fits the technology; otherwise why it does not.</summary>
    public static string? Problem(string technology, string? value)
    {
        var label = TagMappings.ValueLabelOf(technology);
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{label} is missing";
        }

        var hex = Separators().Replace(value, string.Empty);
        return technology switch
        {
            TagTechnology.Ble when !MacAddress().IsMatch(value) =>
                "MAC Address must be 6 hex pairs, e.g. AC:23:3F:93:AF:88",
            TagTechnology.Nfc when !NfcUid().IsMatch(value) =>
                "UID must be 4, 7 or 10 hex pairs, e.g. 04:A2:3B:1C:5D:80:00",
            TagTechnology.PassiveRfid or TagTechnology.ActiveRfid when !HexOnly().IsMatch(hex) || hex.Length % 4 != 0 || hex.Length is < 8 or > 64 =>
                "EPC must be 8 to 64 hex characters in groups of 4, e.g. 3008 33B2 DDD9 0140 0000 0001",
            TagTechnology.Barcode when !PrintableAscii().IsMatch(value) || value.Length > 80 =>
                "Encoded Value of a barcode must be up to 80 printable characters",
            TagTechnology.QrCode when value.Length > 1024 =>
                "Encoded Value of a QR code cannot be longer than 1024 characters",
            _ => null
        };
    }

    [GeneratedRegex(@"[\s:\-\.]")]
    private static partial Regex Separators();

    [GeneratedRegex("^[0-9A-Fa-f]+$")]
    private static partial Regex HexOnly();

    [GeneratedRegex("^([0-9A-F]{2}:){5}[0-9A-F]{2}$")]
    private static partial Regex MacAddress();

    [GeneratedRegex("^([0-9A-F]{2}:){3}[0-9A-F]{2}$|^([0-9A-F]{2}:){6}[0-9A-F]{2}$|^([0-9A-F]{2}:){9}[0-9A-F]{2}$")]
    private static partial Regex NfcUid();

    [GeneratedRegex("^[\x20-\x7E]+$")]
    private static partial Regex PrintableAscii();
}

public static partial class TrackingTagValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;
    private const int NoteMaxLength = 500;

    public static Dictionary<string, string[]> Validate(this RegisterTagRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireOneOf(errors, "Technology", request.Technology, TagTechnology.All, required: true);
        var technology = ContractText.Canonical(request.Technology, TagTechnology.All);

        if (string.IsNullOrWhiteSpace(request.TagNumber))
        {
            errors["TagNumber"] = ["TagNumber is required"];
        }
        else if (!TagNumberPattern().IsMatch(request.TagNumber.Trim()))
        {
            errors["TagNumber"] = ["TagNumber can only contain letters and digits (up to 20), e.g. 002952"];
        }

        if (technology is not null)
        {
            ValidateTagType(errors, technology, request.TagType, required: true);
            ValidateValue(errors, technology, request.Value);
        }

        ValidateDetails(errors, request.FirmwareVersion, request.SiteId, request.SiteName, request.LocationName);
        MaxLength(errors, "AssetCode", request.AssetCode, CodeMaxLength);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateTagRequest request, string technology)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateTagType(errors, technology, request.TagType, required: false);
        if (request.Value is not null)
        {
            ValidateValue(errors, technology, request.Value);
        }

        ValidateDetails(errors, request.FirmwareVersion, request.SiteId, request.SiteName, request.LocationName);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(this AssignTagRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetCode))
        {
            errors["AssetCode"] = ["AssetCode is required"];
        }
        else
        {
            MaxLength(errors, "AssetCode", request.AssetCode, CodeMaxLength);
        }

        ValidateDetails(errors, null, request.SiteId, request.SiteName, request.LocationName);
        MaxLength(errors, "Note", request.Note, NoteMaxLength);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(this DeactivateTagRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors["Reason"] = ["Reason is required"];
        }
        else
        {
            MaxLength(errors, "Reason", request.Reason, NoteMaxLength);
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UnassignTagRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        MaxLength(errors, "Reason", request.Reason, NoteMaxLength);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(this RecordTagReadRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ReadBy))
        {
            errors["ReadBy"] = ["ReadBy is required, e.g. Gateway GW-DXB-04"];
        }
        else
        {
            MaxLength(errors, "ReadBy", request.ReadBy, NameMaxLength);
        }

        if (request.BatteryPercent is < 0 or > 100)
        {
            errors["BatteryPercent"] = ["BatteryPercent must be between 0 and 100"];
        }

        if (request.SignalDbm is < -150 or > 0)
        {
            errors["SignalDbm"] = ["SignalDbm must be between -150 and 0"];
        }

        // Allows a little reader clock drift, but not readings from the future.
        if (request.ReadAt?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors["ReadAt"] = ["ReadAt cannot be in the future"];
        }

        ValidateDetails(errors, null, request.SiteId, request.SiteName, request.LocationName);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(this TagListQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        RequireOneOf(errors, "technology", query.Technology, TagTechnology.All, required: false);
        RequireOneOf(errors, "status", query.Status, TagStatus.All, required: false);
        RequireOneOf(errors, "validation", query.Validation, TagValidation.All, required: false);
        ValidatePaging(errors, query.Page, query.PageSize);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this TagAuditQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        RequireOneOf(errors, "eventType", query.EventType, TagEventType.All, required: false);
        RequireOneOf(errors, "technology", query.Technology, TagTechnology.All, required: false);
        RequireOneOf(errors, "period", query.Period, TagAuditPeriod.All, required: false);
        if (query.From is not null && query.To is not null && query.From > query.To)
        {
            errors["from"] = ["from must be on or before to"];
        }

        ValidatePaging(errors, query.Page, query.PageSize);
        return errors;
    }

    private static void ValidateTagType(Dictionary<string, string[]> errors, string technology, string? tagType, bool required)
    {
        if (string.IsNullOrWhiteSpace(tagType))
        {
            if (required)
            {
                errors["TagType"] = ["TagType is required"];
            }
        }
        else if (ContractText.Canonical(tagType, TagTechnology.TagTypes[technology]) is null)
        {
            errors["TagType"] = [$"TagType of {technology} must be one of: {string.Join(", ", TagTechnology.TagTypes[technology])}"];
        }
    }

    private static void ValidateValue(Dictionary<string, string[]> errors, string technology, string? value)
    {
        if (TagValueFormat.Problem(technology, TagValueFormat.Normalize(technology, value)) is { } problem)
        {
            errors["Value"] = [problem];
        }
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        string? firmwareVersion,
        string? siteId,
        string? siteName,
        string? locationName)
    {
        MaxLength(errors, "FirmwareVersion", firmwareVersion, CodeMaxLength);
        MaxLength(errors, "SiteId", siteId, CodeMaxLength);
        MaxLength(errors, "SiteName", siteName, NameMaxLength);
        MaxLength(errors, "LocationName", locationName, NameMaxLength);
    }

    private static void ValidatePaging(Dictionary<string, string[]> errors, int? page, int? pageSize)
    {
        if (page is < 1)
        {
            errors["page"] = ["page must be 1 or more"];
        }

        if (pageSize is < 1 or > 200)
        {
            errors["pageSize"] = ["pageSize must be between 1 and 200"];
        }
    }

    private static void RequireOneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors[field] = [$"{field} is required"];
            }
        }
        else if (ContractText.Canonical(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }

    [GeneratedRegex("^[A-Za-z0-9]{1,20}$")]
    private static partial Regex TagNumberPattern();
}
