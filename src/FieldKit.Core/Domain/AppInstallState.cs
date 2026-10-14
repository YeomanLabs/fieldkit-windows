namespace FieldKit.Core.Domain;

/// <summary>Intune's install state for one app on one device (Graph's <c>resultantAppState</c>).</summary>
public enum AppInstallState
{
    Unknown,
    Installed,
    Failed,
    NotInstalled,
    UninstallFailed,
    PendingInstall,
    NotApplicable,
}
