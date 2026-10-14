namespace FieldKit.Core.Services;

/// <summary>The PC FieldKit is running on, for the This PC lookup.</summary>
public interface ILocalDeviceInfo
{
    /// <summary>The firmware (SMBIOS) serial number as reported, possibly empty or a placeholder.</summary>
    string? SerialNumber { get; }

    string MachineName { get; }
}
