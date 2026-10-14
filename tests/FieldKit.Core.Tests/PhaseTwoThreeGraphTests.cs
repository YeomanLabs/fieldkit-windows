using System.Text;
using FieldKit.Core.Tests.Support;
using static FieldKit.Core.Tests.Support.ScriptedTransport;

namespace FieldKit.Core.Tests;

public sealed class PhaseTwoThreeGraphTests
{
    private const string EntraId = "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b";

    [Fact]
    public async Task PeopleSearchUsesAdvancedQuery()
    {
        var stub = new StubGraphClient(new() { ["users"] = "users_search" });
        var people = await new DirectoryLookup(stub).SearchUsersAsync("Al\"ex");
        Assert.Equal(["Alex Doe", "svc-kiosk@contoso.com"], people.Select(person => person.DisplayName));
        Assert.Equal("Analyst · Finance", people[0].Subtitle);

        var request = Assert.Single(stub.Sent);
        Assert.Equal("eventual", request.Headers["ConsistencyLevel"]);
        Assert.Equal(
            "\"displayName:Alex\" OR \"userPrincipalName:Alex\" OR \"mail:Alex\"",
            RequestInspection.QueryValue(request.Url, "$search"));
    }

    [Fact]
    public async Task ShortPeopleSearchSendsNothing()
    {
        var stub = new StubGraphClient(new());
        Assert.Empty(await new DirectoryLookup(stub).SearchUsersAsync("a"));
        Assert.Empty(stub.Sent);
    }

    [Fact]
    public async Task PersonDevicesUseTheirManagedDevices()
    {
        var stub = new StubGraphClient(new() { ["users/u-1/managedDevices"] = "managedDevices_single" });
        var devices = await new DirectoryLookup(stub).ManagedDevicesAsync("u-1");
        Assert.Equal(["CTS-LT-01234"], devices.Select(device => device.DeviceName));
    }

    [Fact]
    public async Task DeviceNameSearchUsesStartsWith()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices"] = "managedDevices_single" });
        await new DeviceLookup(stub).SearchDevicesAsync("O'CTS");
        var url = stub.Requests[0].AbsoluteUri;
        Assert.Contains("startswith(deviceName,'O''CTS')", url, StringComparison.Ordinal);
        Assert.Contains("$top=25", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompliancePolicyStatesDecode()
    {
        var stub = new StubGraphClient(new() { ["deviceManagement/managedDevices/i-1/deviceCompliancePolicyStates"] = "compliance_policy_states" });
        var policies = await new DeviceLookup(stub).CompliancePoliciesAsync("i-1");
        Assert.Equal(3, policies.Count);
        var baseline = policies[0];
        Assert.Equal(PolicyComplianceState.Noncompliant, baseline.State);
        Assert.Equal(["bitLockerEnabled", "Minimum OS version"], baseline.FailingSettings.Select(setting => setting.Name));
        Assert.Equal("False", baseline.FailingSettings[0].CurrentValue);
        Assert.Null(baseline.FailingSettings[0].ErrorDescription);
        Assert.Equal("Couldn't evaluate", baseline.FailingSettings[1].ErrorDescription);
        Assert.Empty(policies[2].Settings);
    }

    [Theory]
    [InlineData(DeviceAction.Sync, "syncDevice")]
    [InlineData(DeviceAction.Restart, "rebootNow")]
    [InlineData(DeviceAction.Retire, "retire")]
    public async Task DeviceActionsPostToTheRightEndpoint(DeviceAction action, string endpoint)
    {
        var path = $"deviceManagement/managedDevices/i-1/{endpoint}";
        var stub = new StubGraphClient(new() { [path] = "" });
        await new DeviceActions(stub).PerformAsync(action, "i-1");
        var request = Assert.Single(stub.Sent);
        Assert.Equal(GraphMethod.Post, request.Method);
        Assert.Equal($"/v1.0/{path}", request.Url.AbsolutePath);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task LapsReturnsNewestCredentialWithClientHeaders()
    {
        var stub = new StubGraphClient(new() { [$"directory/deviceLocalCredentials/{EntraId}"] = "device_local_credentials" });
        var credential = Assert.IsType<LocalAdminCredential>(await new RecoverySecrets(stub).LocalAdminCredentialAsync(EntraId));
        Assert.Equal("Kp7#vQ2m-Lx9!dRw", credential.Password);
        Assert.Equal("LocalAdmin", credential.AccountName);
        Assert.Equal("CTS-LT-01234", credential.DeviceName);
        Assert.NotNull(credential.RotatesAt);

        var request = Assert.Single(stub.Sent);
        Assert.Equal("FieldKit", request.Headers["ocp-client-name"]);
        Assert.True(request.Headers.ContainsKey("ocp-client-version"));
        Assert.Contains("$select=id,deviceName,lastBackupDateTime,refreshDateTime,credentials", request.Url.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LapsNotBackedUpIsNull()
    {
        var stub = new StubGraphClient(new() { [$"directory/deviceLocalCredentials/{EntraId}"] = GraphException.NotFound() });
        Assert.Null(await new RecoverySecrets(stub).LocalAdminCredentialAsync(EntraId));
    }

    [Fact]
    public async Task BitLockerFetchesEachKeyNewestFirst()
    {
        var stub = new StubGraphClient(new()
        {
            ["informationProtection/bitlocker/recoveryKeys"] = "bitlocker_keys_list",
            ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-aaaa-4000-8000-000000000001"] = "bitlocker_key_1",
            ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-bbbb-4000-8000-000000000002"] = "bitlocker_key_2",
        });
        var lookup = await new RecoverySecrets(stub).BitLockerKeysAsync(EntraId, "obj-1");
        Assert.Equal(["Fixed data drive", "OS drive"], lookup.Keys.Select(key => key.Volume));
        Assert.Equal("118657-202301-591943-336754-218812-490765-611820-264407", lookup.Keys[1].Key);
        Assert.Equal(2, lookup.ListedCount);
        Assert.Equal([EntraId], lookup.IdsTried);

        var sent = stub.Sent;
        Assert.Equal(3, sent.Count);
        Assert.All(sent, request => Assert.Equal("FieldKit", request.Headers["ocp-client-name"]));
        Assert.Contains($"deviceId%20eq%20'{EntraId}'", sent[0].Url.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("?$select=key", sent[1].Url.Query);
    }

    [Fact]
    public async Task BitLockerFallsBackToObjectId()
    {
        var stub = new StubGraphClient(
            new()
            {
                ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-aaaa-4000-8000-000000000001"] = "bitlocker_key_1",
                ["informationProtection/bitlocker/recoveryKeys/b7f3a1c2-bbbb-4000-8000-000000000002"] = "bitlocker_key_2",
            },
            request =>
            {
                if (!request.Url.AbsolutePath.EndsWith("/recoveryKeys", StringComparison.Ordinal)) return null;
                return request.Url.AbsoluteUri.Contains("obj-1", StringComparison.Ordinal) ? "bitlocker_keys_list" : "autopilot_empty";
            });
        var lookup = await new RecoverySecrets(stub).BitLockerKeysAsync(EntraId, "obj-1");
        Assert.Equal(2, lookup.Keys.Count);
        Assert.Equal([EntraId, "obj-1"], lookup.IdsTried);
    }

    [Fact]
    public async Task BitLockerEmptyReportsWhatWasTried()
    {
        var stub = new StubGraphClient(new() { ["informationProtection/bitlocker/recoveryKeys"] = "autopilot_empty" });
        var lookup = await new RecoverySecrets(stub).BitLockerKeysAsync(EntraId, null);
        Assert.Empty(lookup.Keys);
        Assert.Equal(0, lookup.ListedCount);
        Assert.Equal([EntraId], lookup.IdsTried);
    }

    [Fact]
    public async Task AutopilotUpdatesSendExpectedBodies()
    {
        const string root = "deviceManagement/windowsAutopilotDeviceIdentities/ap-1";
        var stub = new StubGraphClient(new()
        {
            [$"{root}/updateDeviceProperties"] = "",
            [$"{root}/assignUserToDevice"] = "",
            [$"{root}/unassignUserFromDevice"] = "",
        });
        var updates = new AutopilotUpdates(stub);
        await updates.SetGroupTagAsync("CTS-Kiosk", "ap-1");
        await updates.AssignUserAsync("alex.doe@contoso.com", "Alex Doe", "ap-1");
        await updates.UnassignUserAsync("ap-1");

        var sent = stub.Sent;
        Assert.All(sent, request => Assert.Equal(GraphMethod.Post, request.Method));
        Assert.Equal(new Dictionary<string, string> { ["groupTag"] = "CTS-Kiosk" }, RequestInspection.JsonBody(sent[0].Body));
        Assert.Equal(
            new Dictionary<string, string> { ["userPrincipalName"] = "alex.doe@contoso.com", ["addressableUserName"] = "Alex Doe" },
            RequestInspection.JsonBody(sent[1].Body));
        Assert.Null(sent[2].Body);
    }

    [Fact]
    public async Task PostSendsJsonContentType()
    {
        var transport = new ScriptedTransport(Reply.Status(204));
        var client = new GraphClient(new FakeTokenProvider(), transport, (_, _) => Task.CompletedTask);
        await client.SendAsync(GraphRequest.Post("x", new Dictionary<string, string> { ["a"] = "b" }));
        var request = Assert.Single(transport.Sent);
        Assert.Equal("POST", request.Method);
        Assert.Equal("application/json", request.Headers["Content-Type"]);
        Assert.Equal("""{"a":"b"}""", Encoding.UTF8.GetString(request.Body!));
    }
}
