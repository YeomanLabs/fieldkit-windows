namespace FieldKit.Core.Domain;

/// <summary>
/// Windows releases as a Win32 app's minimum OS (<c>minimumSupportedWindowsRelease</c>: "1607",
/// "Windows11_23H2"), with the first build of each. Bare "21H2" and "22H2" are left unjudged: they
/// were releases of both Windows 10 and 11.
/// </summary>
public static class WindowsReleases
{
    private static readonly Dictionary<string, (string Name, int Build)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1607"] = ("Windows 10 1607", 14393),
        ["1703"] = ("Windows 10 1703", 15063),
        ["1709"] = ("Windows 10 1709", 16299),
        ["1803"] = ("Windows 10 1803", 17134),
        ["1809"] = ("Windows 10 1809", 17763),
        ["1903"] = ("Windows 10 1903", 18362),
        ["1909"] = ("Windows 10 1909", 18363),
        ["2004"] = ("Windows 10 2004", 19041),
        ["2H20"] = ("Windows 10 20H2", 19042),
        ["20H2"] = ("Windows 10 20H2", 19042),
        ["21H1"] = ("Windows 10 21H1", 19043),
        ["Windows10_21H2"] = ("Windows 10 21H2", 19044),
        ["Windows10_22H2"] = ("Windows 10 22H2", 19045),
        ["Windows11_21H2"] = ("Windows 11 21H2", 22000),
        ["Windows11_22H2"] = ("Windows 11 22H2", 22621),
        ["Windows11_23H2"] = ("Windows 11 23H2", 22631),
        ["Windows11_24H2"] = ("Windows 11 24H2", 26100),
        ["Windows11_25H2"] = ("Windows 11 25H2", 26200),
    };

    /// <summary>The release's first build, or null when FieldKit doesn't know it for certain.</summary>
    public static int? MinimumBuild(string release) => Known.TryGetValue(release, out var known) ? known.Build : null;

    /// <summary>"Windows 11 23H2"; unknown values come back tidied ("Windows11_26H1" → "Windows 11 26H1").</summary>
    public static string DisplayName(string release) =>
        Known.TryGetValue(release, out var known) ? known.Name
        : release.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) ? release.Replace("Windows1", "Windows 1", StringComparison.OrdinalIgnoreCase).Replace('_', ' ')
        : $"Windows {release}";
}
