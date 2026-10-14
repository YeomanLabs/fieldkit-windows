namespace FieldKit.Core.Domain;

/// <summary>An app's failed install on one device: the error and Intune's own description of it.</summary>
/// <param name="Detail">Intune's text for the failure (<c>AppInstallStateDetails_loc</c>); often empty.</param>
public sealed record AppInstallFailure(int ErrorCode, string Detail, DateTimeOffset? At)
{
    public string HexCode => InstallErrorText.Hex(ErrorCode);

    /// <summary>"0x87D1041C · The app wasn't detected ..." or just the code when FieldKit doesn't know it.</summary>
    public string Summary => ErrorCode == 0
        ? ""
        : InstallErrorText.Meaning(ErrorCode) is { } meaning ? $"{HexCode} · {meaning}" : HexCode;
}
