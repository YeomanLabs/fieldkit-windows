namespace FieldKit.Core.Domain;

/// <summary>How an app is assigned to a device or its user (Graph's <c>mobileAppIntent</c>).</summary>
public enum AppIntent
{
    Unknown,
    Available,
    NotAvailable,
    RequiredInstall,
    RequiredUninstall,
    RequiredAndAvailable,
    AvailableWithoutEnrollment,
    Exclude,
}
