using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Contracts;

/// <param name="MatchedBy">Which identifier matched the scanned code: assetTag, barcode, rfid, nfc or assetRegistrationId.</param>
public sealed record AssetLookupResponse(string Code, string MatchedBy, AssetRegistrationResponse Asset);

public static class AssetLookupCodeTypes
{
    // Checked in this order; the first that matches is reported as MatchedBy.
    public static readonly IReadOnlyList<AssetScanCodeType> All =
    [
        AssetScanCodeType.AssetTag,
        AssetScanCodeType.Barcode,
        AssetScanCodeType.Rfid,
        AssetScanCodeType.Nfc,
        AssetScanCodeType.AssetRegistrationId
    ];

    private static readonly Dictionary<string, AssetScanCodeType> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["assetTag"] = AssetScanCodeType.AssetTag,
        ["tag"] = AssetScanCodeType.AssetTag,
        ["barcode"] = AssetScanCodeType.Barcode,
        ["qr"] = AssetScanCodeType.Barcode,
        ["qrCode"] = AssetScanCodeType.Barcode,
        ["rfid"] = AssetScanCodeType.Rfid,
        ["nfc"] = AssetScanCodeType.Nfc,
        ["assetRegistrationId"] = AssetScanCodeType.AssetRegistrationId,
        ["id"] = AssetScanCodeType.AssetRegistrationId
    };

    public static bool TryParse(string? value, out AssetScanCodeType codeType) =>
        ByName.TryGetValue(value?.Trim() ?? string.Empty, out codeType);

    public static string ToName(AssetScanCodeType codeType) => codeType switch
    {
        AssetScanCodeType.AssetTag => "assetTag",
        AssetScanCodeType.Barcode => "barcode",
        AssetScanCodeType.Rfid => "rfid",
        AssetScanCodeType.Nfc => "nfc",
        _ => "assetRegistrationId"
    };

    /// <summary>The identifier of the given type on the asset, used to work out which one matched.</summary>
    public static string? ValueOf(A360.Asset.Domain.Entities.AssetRegistration asset, AssetScanCodeType codeType) => codeType switch
    {
        AssetScanCodeType.AssetTag => asset.BasicInformation?.AssetTag,
        AssetScanCodeType.Barcode => asset.BasicInformation?.Barcode,
        AssetScanCodeType.Rfid => asset.BasicInformation?.RfidTagId,
        AssetScanCodeType.Nfc => asset.BasicInformation?.NfcUid,
        _ => asset.AssetRegistrationId
    };
}
