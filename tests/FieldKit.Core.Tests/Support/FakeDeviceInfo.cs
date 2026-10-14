using FieldKit.Core.Services;

namespace FieldKit.Core.Tests.Support;

internal sealed class FakeDeviceInfo(string? serialNumber, string machineName = "DESKTOP-TEST") : ILocalDeviceInfo
{
    public string? SerialNumber { get; } = serialNumber;

    public string MachineName { get; } = machineName;
}
