namespace FieldKit.Core.Services;

/// <summary>A device's logs couldn't be read; the message says why and what to try.</summary>
public sealed class DeviceLogException(string message) : Exception(message);
