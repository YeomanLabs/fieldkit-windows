namespace FieldKit.Core.Domain;

/// <summary>An app Intune's inventory found on the device (installed by anyone, not only Intune).</summary>
public sealed record DetectedApp(string Id, string Name, string Version, string Publisher, long? SizeBytes);
