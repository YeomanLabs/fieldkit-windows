namespace FieldKit.Core.Domain;

/// <summary>Where an app stands on a device, in helpdesk terms.</summary>
public enum AppInstallVerdict
{
    Installed,
    Uninstalled,
    Available,
    Waiting,
    NotInstalled,
    NotApplicable,
    Failed,
}
