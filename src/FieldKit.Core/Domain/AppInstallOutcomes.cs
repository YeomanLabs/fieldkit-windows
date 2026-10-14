namespace FieldKit.Core.Domain;

public static class AppInstallOutcomes
{
    /// <summary>
    /// Available apps nobody has installed read "unknown" in Intune (seen on a real tenant), so
    /// they're "available", not a problem. A required app with no state yet is "no status".
    /// </summary>
    public static AppInstallOutcome For(AppIntent intent, AppInstallState state)
    {
        if (state is AppInstallState.Failed or AppInstallState.UninstallFailed) return AppInstallOutcome.Failed;
        if (state == AppInstallState.PendingInstall) return AppInstallOutcome.Pending;
        if (intent == AppIntent.RequiredUninstall)
        {
            return state switch
            {
                AppInstallState.NotInstalled => AppInstallOutcome.Removed,
                AppInstallState.Installed => AppInstallOutcome.Missing,
                AppInstallState.NotApplicable => AppInstallOutcome.NotApplicable,
                _ => AppInstallOutcome.NoStatus,
            };
        }
        return state switch
        {
            AppInstallState.Installed => AppInstallOutcome.Installed,
            AppInstallState.NotApplicable => AppInstallOutcome.NotApplicable,
            _ when intent.IsRequired() => state == AppInstallState.NotInstalled ? AppInstallOutcome.Missing : AppInstallOutcome.NoStatus,
            _ => AppInstallOutcome.Available,
        };
    }

    public static string DisplayName(this AppInstallOutcome outcome, AppIntent intent) =>
        outcome switch
        {
            AppInstallOutcome.Failed => "Failed",
            AppInstallOutcome.Pending => "Pending",
            AppInstallOutcome.Missing => intent == AppIntent.RequiredUninstall ? "Still installed" : "Not installed",
            AppInstallOutcome.NoStatus => "No status yet",
            AppInstallOutcome.Installed => "Installed",
            AppInstallOutcome.Removed => "Uninstalled",
            AppInstallOutcome.Available => "Not installed (available)",
            _ => "Not applicable",
        };
}
