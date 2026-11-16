using System.Text.Json.Nodes;

namespace NetworkServer.Services;

/// <summary>
/// Decodes the tags relayed by a Minew MBM04 location relay beacon (repeater). The repeater advertises a Minew
/// manufacturer frame:
///   FF 39 06 D1 &lt;field flag (2)&gt; &lt;mac/battery/version/pid as set in the field flag&gt; &lt;TLVs&gt; [&lt;random (2)&gt; &lt;MIC (2)&gt;]
/// Its own status frame ("Advertising 0") only has motion / button TLVs; the relay frame ("Advertising 1") has a
/// MAC relay TLV:
///   5E &lt;length&gt; &lt;flag&gt; [&lt;fragment flag&gt;] {[&lt;rssi&gt;] &lt;mac, little-endian&gt;} x count
///   flag: bit7 fragmented (a second flag byte follows), bit5 RSSI present, bits4-3 address length
///         (0 = last 2 bytes, 1 = last 3, 2 = last 4, 3 = full 6), bits2-0 number of relayed tags
/// Example: 0201061AFF3906D11210003A5E1025A30662... -> rssi -93, mac ....:62:06 ("6206").
/// </summary>
public static class RepeaterPayload
{
    private const byte MacRelay = 0x5E;

    /// <summary>
    /// Returns the tags relayed in the repeater's advertisement, in the stored layout. The tagid is the mac as far
    /// as the frame carries it, so usually only its last bytes ("6206"); an advertisement without a relay TLV
    /// (e.g. the status frame) gives none.
    /// </summary>
    public static List<JsonObject> RelayedTags(JsonObject repeater)
    {
        var tags = new List<JsonObject>();
        var raw = (string?)repeater["raw"];
        var buffer = new byte[raw?.Length ?? 0];
        if (raw is null || !Convert.TryFromBase64String(raw, buffer, out var length))
            return tags;
        var adv = buffer.AsSpan(0, length);

        // Advertisement is a list of AD structures: <length> <type> <data...>
        for (var i = 0; i + 1 < adv.Length && adv[i] > 0 && i + adv[i] < adv.Length; i += adv[i] + 1)
        {
            var data = adv.Slice(i + 2, adv[i] - 1);
            if (adv[i + 1] == 0xFF && data.Length >= 5 && data[0] == 0x39 && data[1] == 0x06 && data[2] == 0xD1)
                ReadTlvs(data[3..], raw, tags);
        }
        return tags;
    }

    // userData starts at the field flag: bits 15-11 length of the fields before the TLVs, bit 9 TLVs, bit 8 MIC.
    private static void ReadTlvs(ReadOnlySpan<byte> userData, string raw, List<JsonObject> tags)
    {
        if (userData.Length < 2 || (userData[0] & 0x02) == 0)
            return;
        var end = userData.Length - ((userData[0] & 0x01) != 0 ? 4 : 0);

        // TLV: <type> <length> <value>. A type with bit7 set is 2 bytes (0x8B13); the length byte holds an inline
        // value in bits 6-5 and the value length in bits 4-0.
        for (var i = 2 + (userData[0] >> 3); i + 1 < end;)
        {
            int type = userData[i++];
            if ((type & 0x80) != 0)
                type = (type << 8) | userData[i++];
            if (i >= end || (userData[i] & 0x80) != 0) // extended length is not used by these frames
                return;
            var size = userData[i++] & 0x1F;
            if (i + size > end)
                return;
            if (type == MacRelay)
                ReadMacRelay(userData.Slice(i, size), raw, tags);
            i += size;
        }
    }

    private static void ReadMacRelay(ReadOnlySpan<byte> value, string raw, List<JsonObject> tags)
    {
        if (value.Length == 0)
            return;
        var flag = value[0];
        var hasRssi = (flag & 0x20) != 0;
        var addressLength = ((flag >> 3) & 0x03) switch { 0 => 2, 1 => 3, 2 => 4, _ => 6 };
        // A fragmented batch has a second flag byte (fragment index / last fragment); each fragment is read on its own.
        var i = (flag & 0x80) != 0 ? 2 : 1;

        for (var n = 0; n < (flag & 0x07) && i + (hasRssi ? 1 : 0) + addressLength <= value.Length; n++)
        {
            int? rssi = hasRssi ? (sbyte)value[i++] : null; // hex - 256
            var mac = value.Slice(i, addressLength).ToArray();
            Array.Reverse(mac); // little-endian
            i += addressLength;

            tags.Add(new JsonObject
            {
                ["tagid"] = Convert.ToHexString(mac).ToLowerInvariant(),
                ["rssi"] = rssi,
                ["raw"] = raw,
                ["batterypercentage"] = null,
                ["device_trigger"] = null,
                ["status"] = "online",
            });
        }
    }
}
