namespace FieldKit.Core.Domain;

/// <summary>What an app's assignment and state add up to, worst first.</summary>
public enum AppInstallOutcome
{
    Failed,
    Pending,
    Missing,
    NoStatus,
    Installed,
    Removed,
    Available,
    NotApplicable,
}
