namespace FieldKit.Core.Domain;

/// <summary>Reads an <see cref="AppPlatform"/> from a device's OS or a Graph app type.</summary>
public static class AppPlatformParser
{
    public static AppPlatform? FromOperatingSystem(string operatingSystem) =>
        operatingSystem.ToLowerInvariant() switch
        {
            "windows" => AppPlatform.Windows,
            "ios" or "ipados" => AppPlatform.iOS,
            "android" => AppPlatform.Android,
            "macos" => AppPlatform.macOS,
            _ => null,
        };

    /// <summary>
    /// From a Graph app type such as "win32LobApp" or "iosVppApp". Null for cross-platform
    /// types (web links) and unknown ones.
    /// </summary>
    public static AppPlatform? FromAppType(string appType)
    {
        var type = appType.Replace("#microsoft.graph.", "", StringComparison.Ordinal).ToLowerInvariant();
        if (type.StartsWith("ios", StringComparison.Ordinal) || type.StartsWith("managedios", StringComparison.Ordinal))
            return AppPlatform.iOS;
        if (type.StartsWith("android", StringComparison.Ordinal) || type.StartsWith("managedandroid", StringComparison.Ordinal))
            return AppPlatform.Android;
        if (type.StartsWith("macos", StringComparison.Ordinal))
            return AppPlatform.macOS;
        if (type.StartsWith("win", StringComparison.Ordinal) || type == "officesuiteapp"
            || type.StartsWith("microsoftstore", StringComparison.Ordinal))
            return AppPlatform.Windows;
        return null;
    }
}
