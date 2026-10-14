namespace FieldKit.Core.Domain;

public static class AppIntents
{
    public static AppIntent FromRaw(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "available" => AppIntent.Available,
            "notavailable" => AppIntent.NotAvailable,
            "requiredinstall" => AppIntent.RequiredInstall,
            "requireduninstall" => AppIntent.RequiredUninstall,
            "requiredandavailableinstall" => AppIntent.RequiredAndAvailable,
            "availableinstallwithoutenrollment" => AppIntent.AvailableWithoutEnrollment,
            "exclude" => AppIntent.Exclude,
            _ => AppIntent.Unknown,
        };

    public static string DisplayName(this AppIntent intent) =>
        intent switch
        {
            AppIntent.Available or AppIntent.AvailableWithoutEnrollment => "Available",
            AppIntent.RequiredInstall => "Required",
            AppIntent.RequiredUninstall => "Required uninstall",
            AppIntent.RequiredAndAvailable => "Required and available",
            AppIntent.Exclude => "Excluded",
            AppIntent.NotAvailable => "Not available",
            _ => "Assigned",
        };

    /// <summary>Intune installs it without the user asking.</summary>
    public static bool IsRequired(this AppIntent intent) => intent is AppIntent.RequiredInstall or AppIntent.RequiredAndAvailable;
}
