namespace FieldKit.Core.Domain;

public static class AppInstallStates
{
    /// <summary>From <c>mobileAppIntentAndStates</c>' <c>installState</c> text.</summary>
    public static AppInstallState FromRaw(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "installed" => AppInstallState.Installed,
            "failed" => AppInstallState.Failed,
            "notinstalled" => AppInstallState.NotInstalled,
            "uninstallfailed" => AppInstallState.UninstallFailed,
            "pendinginstall" => AppInstallState.PendingInstall,
            "notapplicable" => AppInstallState.NotApplicable,
            _ => AppInstallState.Unknown,
        };

    /// <summary>The report's <c>InstallState</c> number for a state, for filtering.</summary>
    public static int ReportCode(this AppInstallState state) =>
        state switch
        {
            AppInstallState.Installed => 1,
            AppInstallState.Failed => 2,
            AppInstallState.NotInstalled => 3,
            AppInstallState.UninstallFailed => 4,
            AppInstallState.PendingInstall => 5,
            AppInstallState.NotApplicable => -1,
            _ => 99,
        };
}
