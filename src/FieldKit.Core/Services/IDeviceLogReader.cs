using FieldKit.Core.Domain;

namespace FieldKit.Core.Services;

/// <summary>
/// Reads Intune Management Extension logs the engineer opens: the zip from the device's Collect
/// diagnostics in Intune, or log files copied from the device (AppWorkload.log). FieldKit never
/// reaches into the device itself. Failures throw <see cref="DeviceLogException"/> with text to show.
/// </summary>
public interface IDeviceLogReader
{
    /// <summary>Zips and .log files, in any mix; zips are searched for the extension's logs wherever they sit.</summary>
    Task<DeviceLogFiles> FromFilesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);
}
