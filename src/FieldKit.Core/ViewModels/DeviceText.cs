using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>How devices and their details read on the pages (the formatting iOS keeps in its views).</summary>
public static class DeviceText
{
    /// <summary>Display name over UPN, or whichever exists.</summary>
    public static string PrimaryUser(IntuneDevice device) =>
        (device.UserDisplayName.Length == 0, device.UserPrincipalName.Length == 0) switch
        {
            (_, true) => "",
            (true, false) => device.UserPrincipalName,
            _ => $"{device.UserDisplayName}\n{device.UserPrincipalName}",
        };

    public static string OperatingSystem(IntuneDevice device) => Join(" ", device.OperatingSystem, device.OsVersion);

    public static string Model(IntuneDevice device) => Join(" ", device.Manufacturer, device.Model);

    public static string Encrypted(IntuneDevice device) => device.IsEncrypted is { } encrypted ? (encrypted ? "Yes" : "No") : "Unknown";

    public static string Name(IntuneDevice device) => device.DeviceName.Length == 0 ? "Unnamed device" : device.DeviceName;

    /// <summary>"Alex Doe · 5CG1234XYZ", under a device's name in lists.</summary>
    public static string ListSubtitle(IntuneDevice device) => Join(" · ", device.UserDisplayName, device.SerialNumber);

    public static string Synced(IntuneDevice device) =>
        device.LastSyncDateTime is { } synced ? $"Synced {DateText.Relative(synced)}" : "Never synced";

    /// <summary>One choice in the duplicate-records picker: "CTS-LT-01234 (Oct 7, 2026)".</summary>
    public static string RecordChoice(IntuneDevice device) =>
        $"{Name(device)} ({(device.LastSyncDateTime is { } synced ? $"synced {DateText.ShortDate(synced)}" : "never synced")})";

    /// <summary>The serial a device page can look up, or null when Intune's serial isn't usable.</summary>
    public static string? LookupSerial(IntuneDevice device) => SerialNormalizer.Normalize(device.SerialNumber);

    /// <summary>"Current value: False · Couldn't evaluate", or empty.</summary>
    public static string SettingDetail(CompliancePolicySetting setting) =>
        Join(" · ", setting.CurrentValue is { } value ? $"Current value: {value}" : "", setting.ErrorDescription ?? "");

    public static string Join(string separator, params string[] parts) =>
        string.Join(separator, parts.Where(part => part.Length > 0));
}
