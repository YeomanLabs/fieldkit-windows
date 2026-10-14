using System.Text;

namespace FieldKit.Core.Domain;

/// <summary>A Windows LAPS password backed up to Entra. Shown only after Windows Hello; never stored or logged.</summary>
/// <param name="RotatesAt">When Windows LAPS will next rotate the password.</param>
public sealed record LocalAdminCredential(
    string DeviceName,
    string AccountName,
    string Password,
    DateTimeOffset? BackedUpAt,
    DateTimeOffset? RotatesAt)
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16LittleEndian = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Entra returns the password base64-encoded. Accepts UTF-8, and UTF-16LE (the form in
    /// Microsoft's documentation examples).
    /// </summary>
    public static string? DecodePassword(string base64)
    {
        var buffer = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, buffer, out var length) || length == 0) return null;
        var bytes = buffer.AsSpan(0, length);

        var looksUtf16LittleEndian = length % 2 == 0;
        for (var index = 1; index < length && looksUtf16LittleEndian; index += 2)
        {
            looksUtf16LittleEndian = bytes[index] == 0;
        }
        try
        {
            return looksUtf16LittleEndian ? StrictUtf16LittleEndian.GetString(bytes) : StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>Leaves the password out, so logging the record can't leak it.</summary>
    public override string ToString() =>
        $"LocalAdminCredential {{ DeviceName = {DeviceName}, AccountName = {AccountName} }}";
}
