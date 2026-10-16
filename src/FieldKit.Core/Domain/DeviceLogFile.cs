namespace FieldKit.Core.Domain;

/// <summary>One Intune Management Extension log read from a device, e.g. "AppWorkload.log".</summary>
public sealed record DeviceLogFile(string Name, string Text);
