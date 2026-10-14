using System.Text;
using FieldKit.Core.Tests.Support;
using static FieldKit.Core.Tests.Support.ScriptedTransport;

namespace FieldKit.Core.Tests;

public sealed class GraphDecodingTests
{
    private const string LapsJson =
        """{"deviceName":"PC1","credentials":[{"accountName":"LocalAdmin","passwordBase64":"S3A3I3ZRMm0tTHg5IWRSdw=="}]}""";

    private static T Decode<T>(string fixture) => GraphJson.Decode<T>(Fixture.Data(fixture));

    [Fact]
    public void ManagedDevice()
    {
        var page = Decode<GraphCollection<ManagedDeviceDto>>("managedDevices_single");
        var device = Assert.Single(page.Value).ToDomain();
        Assert.Null(page.NextLink);
        Assert.Equal("CTS-LT-01234", device.DeviceName);
        Assert.Equal("alex.doe@contoso.com", device.UserPrincipalName);
        Assert.Equal(ComplianceState.Compliant, device.ComplianceState);
        Assert.Equal("Corporate", device.OwnershipDisplayName);
        Assert.True(device.HasEntraDeviceId);
        Assert.True(device.IsEncrypted);
        var sync = Assert.NotNull(device.LastSyncDateTime);
        Assert.True(Math.Abs(sync.ToUnixTimeMilliseconds() / 1000.0 - 1_791_555_751.123) < 0.001);
        Assert.NotNull(device.EnrolledDateTime);
    }

    [Fact]
    public void DuplicateManagedDevicesWithNullsAndSentinelDates()
    {
        var devices = Decode<GraphCollection<ManagedDeviceDto>>("managedDevices_duplicate").Value.Select(item => item.ToDomain()).ToList();
        Assert.Equal(2, devices.Count);
        Assert.Equal("", devices[0].UserDisplayName);
        Assert.Null(devices[0].EnrolledDateTime);          // 0001-01-01 means never
        Assert.False(devices[0].HasEntraDeviceId);         // all-zero GUID
        Assert.Equal(ComplianceState.Noncompliant, devices[0].ComplianceState);
        Assert.Equal(ComplianceState.InGracePeriod, devices[1].ComplianceState);
    }

    [Fact]
    public void EmptyCollection()
    {
        Assert.Empty(Decode<GraphCollection<ManagedDeviceDto>>("managedDevices_empty").Value);
    }

    [Fact]
    public void EntraDevice()
    {
        var device = Decode<GraphCollection<EntraDeviceDto>>("devices_entra").Value[0].ToDomain();
        Assert.Equal(EntraTrustType.HybridJoined, device.TrustType);
        Assert.Equal("Hybrid joined", device.TrustType.DisplayName);
        Assert.True(device.AccountEnabled);
        Assert.NotNull(device.ApproximateLastSignIn);

        var disabled = Decode<GraphCollection<EntraDeviceDto>>("devices_entra_disabled").Value[0].ToDomain();
        Assert.False(disabled.AccountEnabled);
        Assert.Equal("Entra joined", disabled.TrustType.DisplayName);
        Assert.Null(disabled.ApproximateLastSignIn);
    }

    [Fact]
    public void AutopilotIdentity()
    {
        var records = Decode<GraphCollection<AutopilotDeviceIdentityDto>>("autopilot_identities").Value.Select(item => item.ToDomain()).ToList();
        Assert.Equal(2, records.Count);
        Assert.Equal("CTS-Standard", records[0].GroupTag);
        Assert.Equal("alex.doe@contoso.com", records[0].AssignedUser);
        Assert.Equal(AutopilotEnrollmentState.Enrolled, records[0].EnrollmentState);
        Assert.NotNull(records[0].LastContacted);
        Assert.Equal("Not contacted", records[1].EnrollmentState.DisplayName);
        Assert.Null(records[1].LastContacted);
    }

    [Fact]
    public void NextLink()
    {
        var page = Decode<GraphCollection<AutopilotDeviceIdentityDto>>("page1_nextlink");
        Assert.Contains("skiptoken", page.NextLink, StringComparison.Ordinal);
    }

    [Fact]
    public void OrganizationAndErrorBodies()
    {
        var organizations = Decode<GraphCollection<OrganizationDto>>("organization");
        Assert.Equal("Contoso", organizations.Value[0].DisplayName);
        var error = Decode<GraphErrorBody>("error_forbidden");
        Assert.Equal("Forbidden", error.Error.Code);
    }

    [Theory]
    [InlineData("2026-10-09T14:22:31Z")]
    [InlineData("2026-10-09T14:22:31.1Z")]
    [InlineData("2026-10-09T14:22:31.123Z")]
    [InlineData("2026-10-09T14:22:31.1234567Z")]
    [InlineData("2026-10-09T15:22:31.1234567+01:00")]
    public void ParsesGraphDateVariants(string text)
    {
        var date = Assert.NotNull(GraphJson.ParseDate(text));
        Assert.True(Math.Abs(date.ToUnixTimeSeconds() - 1_791_555_751) < 1);
    }

    [Fact]
    public void RejectsGarbageDates()
    {
        Assert.Null(GraphJson.ParseDate("yesterday"));
        Assert.Null(GraphJson.ParseDate(""));
    }

    [Fact]
    public void AcceptsDatesWithoutTimeZoneAsUtc()
    {
        var date = Assert.NotNull(GraphJson.ParseDate("2026-10-09T14:22:31.1234567"));
        Assert.True(Math.Abs(date.ToUnixTimeSeconds() - 1_791_555_751) < 1);
        Assert.NotNull(GraphJson.ParseDate("2026-10-09"));
    }

    [Fact]
    public void OddDatesDoNotSinkTheResponse()
    {
        var json = """
            {"deviceName":"PC1","lastBackupDateTime":"","refreshDateTime":"soon",
             "credentials":[{"accountName":"LocalAdmin","backupDateTime":"2026-10-09T13:45:30","passwordBase64":"S3A3I3ZRMm0tTHg5IWRSdw=="}]}
            """;
        var dto = GraphJson.Decode<DeviceLocalCredentialInfoDto>(Encoding.UTF8.GetBytes(json));
        var credential = Assert.IsType<LocalAdminCredential>(dto.Newest());
        Assert.Equal("Kp7#vQ2m-Lx9!dRw", credential.Password);
        Assert.NotNull(credential.BackedUpAt);
        Assert.Null(credential.RotatesAt);
    }

    [Fact]
    public void ByteOrderMarkAndUtf16BodiesDecode()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(LapsJson)];
        byte[] utf16 = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(LapsJson)];
        var utf16NoBom = Encoding.Unicode.GetBytes(LapsJson);
        foreach (var body in new[] { bom, utf16, utf16NoBom })
        {
            var dto = GraphJson.Decode<DeviceLocalCredentialInfoDto>(GraphJson.Normalized(body));
            Assert.Equal("Kp7#vQ2m-Lx9!dRw", dto.Newest()?.Password);
        }
        Assert.Equal(Encoding.UTF8.GetBytes(LapsJson), GraphJson.Normalized(Encoding.UTF8.GetBytes(LapsJson)));
    }

    [Fact]
    public async Task LapsThroughTheClientWithByteOrderMark()
    {
        byte[] body = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(LapsJson)];
        var transport = new ScriptedTransport(Reply.Status(200, body));
        var client = new GraphClient(new FakeTokenProvider(), transport, (_, _) => Task.CompletedTask);
        var credential = await new RecoverySecrets(client).LocalAdminCredentialAsync("d1");
        Assert.Equal("Kp7#vQ2m-Lx9!dRw", credential?.Password);
    }

    [Fact]
    public async Task DecodingErrorsNameTheFieldNotTheValue()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices"] = "bad_shape" });
        var error = await Assert.ThrowsAsync<GraphException>(() => new DeviceLookup(stub).IntuneDevicesAsync("X1234"));
        Assert.True(error.Matches(GraphException.Decoding("missing value.0.id")), error.Detail);
        Assert.Contains("missing value.0.id", GraphException.Decoding("missing value.0.id").UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void GrantedPermissionsFromTokenScopes()
    {
        string[] scopes =
        [
            "https://graph.microsoft.com/User.Read",
            "https://graph.microsoft.com/BitlockerKey.Read.All",
            "https://graph.microsoft.com/DeviceManagementServiceConfig.ReadWrite.All",
            "openid", "profile", "offline_access",
        ];
        var granted = Assert.IsAssignableFrom<IReadOnlySet<string>>(GraphPermission.Granted(scopes));
        Assert.True(granted.SetEquals(["User.Read", "BitlockerKey.Read.All", "DeviceManagementServiceConfig.ReadWrite.All"]));
        // ReadWrite also covers the matching Read permission.
        Assert.Contains("User.Read.All", GraphPermission.Granted(["User.ReadWrite.All"]) ?? new HashSet<string>());
        Assert.Null(GraphPermission.Granted([]));
        Assert.Null(GraphPermission.Granted(["https://graph.microsoft.com/.default"]));
    }

    [Fact]
    public void ODataEscaping()
    {
        Assert.Equal("'ABC'", ODataLiteral.String("ABC"));
        Assert.Equal("'O''BRIEN''S'", ODataLiteral.String("O'BRIEN'S"));
    }

    [Fact]
    public void RequestEncodesFilter()
    {
        var request = GraphRequest.Get("deviceManagement/managedDevices", [("$filter", "serialNumber eq 'A''B'")]);
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/deviceManagement/managedDevices?$filter=serialNumber%20eq%20'A''B'",
            request.Url.AbsoluteUri);
    }
}
