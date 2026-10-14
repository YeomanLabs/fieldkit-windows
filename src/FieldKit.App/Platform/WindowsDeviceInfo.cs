using System.Diagnostics;
using FieldKit.Core.Services;
using Windows.System.Profile.SystemManufacturers;

namespace FieldKit.App.Platform;

/// <summary>This PC's firmware serial (SMBIOS) and name, for the This PC lookup.</summary>
internal sealed class WindowsDeviceInfo : ILocalDeviceInfo
{
    private readonly Lazy<string?> _serialNumber = new(ReadSerialNumber);

    public string? SerialNumber => _serialNumber.Value;

    public string MachineName => Environment.MachineName;

    private static string? ReadSerialNumber()
    {
        try
        {
            return SmbiosInformation.SerialNumber;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"SMBIOS serial unavailable: {error.GetType().Name}");
            return null;
        }
    }
}
