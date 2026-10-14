namespace FieldKit.Core.Domain;

/// <summary>One app targeted at a device or its user, and whether it installed (Windows only, 2026-10-14).</summary>
public sealed record AppInstallStatus
{
    public string AppId { get; init; } = "";

    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public AppIntent Intent { get; init; }

    public AppInstallState State { get; init; }

    /// <summary>Why it failed, from Intune's per-app report; null until looked up, or when Intune had no row.</summary>
    public AppInstallFailure? Failure { get; init; }

    public AppInstallOutcome Outcome => AppInstallOutcomes.For(Intent, State);
}
