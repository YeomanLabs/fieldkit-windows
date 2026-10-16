namespace FieldKit.Core.Domain;

/// <summary>Intune app types (Graph's <c>@odata.type</c> without the namespace) as people know them.</summary>
public static class AppTypeNames
{
    public const string Win32 = "win32LobApp";

    /// <summary>"Windows app (Win32)" for "win32LobApp"; unknown types come back as they are.</summary>
    public static string Label(string typeName) =>
        typeName switch
        {
            Win32 => "Windows app (Win32)",
            "winGetApp" => "WinGet app",
            "windowsMicrosoftEdgeApp" => "Microsoft Edge",
            "officeSuiteApp" => "Microsoft 365 Apps",
            "windowsMobileMSI" => "Windows MSI",
            "windowsUniversalAppX" or "windowsAppX" => "Windows AppX",
            "microsoftStoreForBusinessApp" or "windowsStoreApp" => "Microsoft Store",
            "iosStoreApp" or "iosVppApp" => "iOS store app",
            "iosLobApp" => "iOS line-of-business",
            "managedIOSStoreApp" or "managedAndroidStoreApp" => "Managed store app",
            "androidManagedStoreApp" or "androidStoreApp" => "Android store app",
            "macOSLobApp" or "macOSDmgApp" or "macOSPkgApp" => "macOS app",
            "webApp" => "Web link",
            _ => typeName,
        };
}
