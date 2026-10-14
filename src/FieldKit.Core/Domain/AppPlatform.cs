namespace FieldKit.Core.Domain;

/// <summary>
/// The platform an Intune app targets, used to offer only apps that fit a device.
/// <see cref="AppPlatformParser"/> reads it from Graph values.
/// </summary>
public enum AppPlatform
{
    Windows,
    iOS,
    Android,
    macOS,
}
