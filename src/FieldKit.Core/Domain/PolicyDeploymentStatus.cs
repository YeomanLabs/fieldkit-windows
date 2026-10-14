namespace FieldKit.Core.Domain;

/// <summary>
/// A configuration profile or policy's result on one device (Windows only, 2026-10-14). Intune
/// reports a row per context (device, and each user); this is all of them, worst result kept.
/// </summary>
/// <param name="TypeName">Intune's own label: "Settings Catalog", "BitLocker", "Software Updates" ...</param>
public sealed record PolicyDeploymentStatus(string Id, string Name, string TypeName, PolicyDeploymentState State, DateTimeOffset? LastReported);
