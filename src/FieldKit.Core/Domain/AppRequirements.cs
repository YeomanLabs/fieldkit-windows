namespace FieldKit.Core.Domain;

/// <summary>
/// Checks a Win32 app's built-in requirements against what Intune last heard from the device. A
/// requirement is only judged when both sides are known for certain; requirement rules (file,
/// registry, script) run on the device, so they're listed, not judged.
/// </summary>
public static class AppRequirements
{
    private const long Megabyte = 1024 * 1024;

    public static IReadOnlyList<AppRequirementCheck> Check(Win32AppSettings app, AppDeviceFacts device)
    {
        var checks = new List<AppRequirementCheck>();
        if (app.MinimumWindowsRelease.Length > 0)
        {
            var minimum = WindowsReleases.MinimumBuild(app.MinimumWindowsRelease);
            var build = device.Build;
            checks.Add(new AppRequirementCheck(
                "Windows version",
                $"{WindowsReleases.DisplayName(app.MinimumWindowsRelease)} or later",
                build is { } known ? $"build {known}" : NotReported,
                minimum is { } needed && build is { } has ? has >= needed : null));
        }
        if (app.Architectures.Count > 0)
        {
            checks.Add(new AppRequirementCheck(
                "Architecture",
                string.Join(", ", app.Architectures.Select(ArchitectureName)),
                device.Architecture is { } arch ? ArchitectureName(arch) : NotReported,
                ArchitectureMet(app, device.Architecture)));
        }
        if (app.MinimumFreeDiskSpaceMb is int disk and > 0)
        {
            checks.Add(new AppRequirementCheck(
                "Free disk space",
                $"{StorageUsage.Format(disk * Megabyte)} free",
                device.FreeBytes is { } free ? $"{StorageUsage.Format(free)} free" : NotReported,
                device.FreeBytes is { } bytes ? bytes >= disk * Megabyte : null));
        }
        if (app.MinimumMemoryMb is int memory and > 0)
        {
            checks.Add(new AppRequirementCheck(
                "Memory",
                StorageUsage.Format(memory * Megabyte),
                device.MemoryBytes is { } total ? StorageUsage.Format(total) : NotReported,
                device.MemoryBytes is { } bytes ? bytes >= memory * Megabyte : null));
        }
        if (app.MinimumProcessors is int processors and > 0)
        {
            checks.Add(new AppRequirementCheck("Processors", $"at least {processors}", NotReported, null));
        }
        if (app.MinimumCpuSpeedMhz is int speed and > 0)
        {
            checks.Add(new AppRequirementCheck("Processor speed", $"at least {speed} MHz", NotReported, null));
        }
        return checks;
    }

    public const string NotReported = "not reported to Intune";

    public static string ArchitectureName(string architecture) =>
        architecture.ToLowerInvariant() switch
        {
            "x64" => "x64",
            "x86" => "x86 (32-bit)",
            "arm64" => "ARM64",
            "arm" => "ARM",
            _ => architecture,
        };

    /// <summary>
    /// Intune treats a device whose architecture isn't selected as not applicable. The older
    /// setting has no ARM64, so an ARM64 device isn't judged against it.
    /// </summary>
    private static bool? ArchitectureMet(Win32AppSettings app, string? device)
    {
        if (device is null) return null;
        if (app.ArchitecturesAreLegacy && device == "arm64") return null;
        return app.Architectures.Contains(device, StringComparer.OrdinalIgnoreCase);
    }
}
