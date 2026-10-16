namespace FieldKit.Core.Domain;

/// <summary>What FieldKit knows about a device when judging an app's requirements, from Intune's last report.</summary>
public sealed record AppDeviceFacts
{
    public string Name { get; init; } = "";

    /// <summary>"10.0.26100.4652".</summary>
    public string OsVersion { get; init; } = "";

    /// <summary>"x64", "arm64" or "x86"; null when Intune hasn't reported it.</summary>
    public string? Architecture { get; init; }

    public long? FreeBytes { get; init; }

    public long? MemoryBytes { get; init; }

    public DateTimeOffset? LastSync { get; init; }

    public string UserName { get; init; } = "";

    public bool HasUser => UserName.Length > 0;

    /// <summary>The Windows build number (26100 in 10.0.26100.4652), or null.</summary>
    public int? Build =>
        OsVersion.Split('.') is { Length: >= 3 } parts && int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var build)
            ? build
            : null;

    /// <param name="hardware">Architecture and memory come from beta hardware details; null when they couldn't be read.</param>
    public static AppDeviceFacts From(IntuneDevice device, HardwareDetails? hardware) =>
        new()
        {
            Name = device.DeviceName,
            OsVersion = device.OsVersion,
            Architecture = hardware?.ProcessorArchitecture?.ToLowerInvariant(),
            FreeBytes = device.Storage?.FreeBytes,
            MemoryBytes = hardware?.PhysicalMemoryBytes,
            LastSync = device.LastSyncDateTime,
            UserName = device.UserDisplayName.Length > 0 ? device.UserDisplayName : device.UserPrincipalName,
        };
}
