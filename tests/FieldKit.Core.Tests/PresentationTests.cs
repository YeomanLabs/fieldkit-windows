using System.Globalization;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows: dates, device text and the This PC serial check.</summary>
public sealed class PresentationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_791_555_751); // 2026-10-09 14:22:31Z

    [Theory]
    [InlineData(-20, "just now")]
    [InlineData(-60 * 5, "5 minutes ago")]
    [InlineData(-60 * 60, "1 hour ago")]
    [InlineData(-60 * 60 * 30, "yesterday")]
    [InlineData(-60 * 60 * 24 * 3, "3 days ago")]
    [InlineData(-60 * 60 * 24 * 15, "2 weeks ago")]
    [InlineData(-60 * 60 * 24 * 50, "1 month ago")]
    [InlineData(-60 * 60 * 24 * 800, "2 years ago")]
    [InlineData(60 * 60 * 24 * 20, "in 2 weeks")]
    [InlineData(60 * 60 * 30, "tomorrow")]
    public void RelativeDates(int offsetSeconds, string expected)
    {
        Assert.Equal(expected, DateText.Relative(Now.AddSeconds(offsetSeconds), Now));
    }

    [Fact]
    public void AbsoluteDatesFollowTheCulture()
    {
        Assert.Equal("Oct 9, 2026 at 2:22 PM", DateText.Absolute(Now, CultureInfo.GetCultureInfo("en-US"), TimeZoneInfo.Utc));
        Assert.Equal("9 Oct 2026 at 14:22", DateText.Absolute(Now, CultureInfo.GetCultureInfo("en-GB"), TimeZoneInfo.Utc));
        Assert.Equal("—", DateText.Absolute(null));
        Assert.Equal("Never", DateText.RelativeAndAbsolute(null));
        Assert.StartsWith("3 days ago · ", DateText.RelativeAndAbsolute(Now.AddDays(-3), Now), StringComparison.Ordinal);
    }

    [Fact]
    public void DeviceText()
    {
        var device = new IntuneDevice
        {
            Id = "i-1", DeviceName = "", SerialNumber = "5CG1234XYZ", UserDisplayName = "Alex Doe",
            UserPrincipalName = "alex@contoso.com", OperatingSystem = "Windows", OsVersion = "10.0.26100",
            Manufacturer = "HP", Model = "", IsEncrypted = null,
        };
        Assert.Equal("Alex Doe\nalex@contoso.com", ViewModels.DeviceText.PrimaryUser(device));
        Assert.Equal("alex@contoso.com", ViewModels.DeviceText.PrimaryUser(device with { UserDisplayName = "" }));
        Assert.Equal("", ViewModels.DeviceText.PrimaryUser(device with { UserPrincipalName = "" }));
        Assert.Equal("Windows 10.0.26100", ViewModels.DeviceText.OperatingSystem(device));
        Assert.Equal("HP", ViewModels.DeviceText.Model(device));
        Assert.Equal("Unknown", ViewModels.DeviceText.Encrypted(device));
        Assert.Equal("Unnamed device", ViewModels.DeviceText.Name(device));
        Assert.Equal("Alex Doe · 5CG1234XYZ", ViewModels.DeviceText.ListSubtitle(device));
        Assert.Equal("Never synced", ViewModels.DeviceText.Synced(device));
        Assert.Equal("5CG1234XYZ", ViewModels.DeviceText.LookupSerial(device));
        Assert.Null(ViewModels.DeviceText.LookupSerial(device with { SerialNumber = "" }));

        var setting = new CompliancePolicySetting("BitLocker", PolicyComplianceState.Noncompliant, "False", "Couldn't evaluate");
        Assert.Equal("Current value: False · Couldn't evaluate", ViewModels.DeviceText.SettingDetail(setting));
        Assert.Equal("", ViewModels.DeviceText.SettingDetail(setting with { CurrentValue = null, ErrorDescription = null }));
    }

    [Theory]
    [InlineData("5CG1234XYZ", "5CG1234XYZ")]
    [InlineData("  pf2abcde ", "PF2ABCDE")]
    public void ThisPcSerialsThatCanBeLookedUp(string raw, string expected)
    {
        Assert.Equal(expected, LocalDeviceSerial.Usable(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("To be filled by O.E.M.")]
    [InlineData("Default string")]
    [InlineData("System Serial Number")]
    [InlineData("0")]
    [InlineData("00000000")]
    [InlineData("0000-0000-0000")]
    [InlineData("123456789")]
    [InlineData("INVALID")]
    [InlineData("VMware-56 4d 7a 1b 2c 3d 4e 5f-60 71 82 93 a4 b5 c6 d7")]
    [InlineData("4637-6614-3398-1234-5678-0123-45")] // Hyper-V: 32 characters, over the spec's 30
    public void ThisPcHidesPlaceholderSerials(string? raw)
    {
        Assert.Null(LocalDeviceSerial.Usable(raw));
    }
}
