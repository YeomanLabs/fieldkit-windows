namespace FieldKit.Core.Domain;

/// <summary>
/// App install error codes as Intune shows them (0x87D1041C), with a meaning for the few that
/// are certain: Intune's detection failure and the standard Windows Installer exit codes.
/// </summary>
public static class InstallErrorText
{
    public static string Hex(int code) => $"0x{unchecked((uint)code):X8}";

    /// <summary>A plain meaning, or null when FieldKit doesn't know the code.</summary>
    public static string? Meaning(int code)
    {
        var value = unchecked((uint)code);
        if (value == 0x87D1041C) return "The app wasn't detected after the installer finished: check the app's detection rule.";
        // 0x8007xxxx is a Windows (Win32) error, which is how Windows Installer exit codes arrive.
        if ((value & 0xFFFF0000) != 0x80070000) return null;
        return (value & 0xFFFF) switch
        {
            1602 => "Windows Installer: the installation was cancelled.",
            1603 => "Windows Installer: fatal error during installation.",
            1618 => "Windows Installer: another installation was already in progress.",
            1619 => "Windows Installer: the installer package couldn't be opened.",
            1625 => "Windows Installer: blocked by system policy.",
            1638 => "Windows Installer: another version of the product is already installed.",
            3010 => "Windows Installer: a restart is needed to finish.",
            var win32 => $"Windows error {win32}.",
        };
    }
}
