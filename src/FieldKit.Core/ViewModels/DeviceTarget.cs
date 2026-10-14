namespace FieldKit.Core.ViewModels;

/// <summary>A device to open: its serial, and the Intune record when it's already known.</summary>
public sealed record DeviceTarget(string Serial, string? IntuneId);
