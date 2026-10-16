using System.Text;
using System.Text.Json;
using FieldKit.Core.Demo;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows (2026-10-14): did it install? App and configuration status for one device.</summary>
public sealed class InstallStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly IntuneDevice Device = new()
    {
        Id = "i-1", DeviceName = "CTS-LT-01234", SerialNumber = "5CG1234XYZ", OperatingSystem = "Windows", UserId = "u-1",
        ComplianceState = ComplianceState.Compliant,
    };

    private static string BodyOf(GraphRequest request) => Encoding.UTF8.GetString(request.Body ?? []);

    [Fact]
    public async Task AppsComeFromTheDeviceAndUsersIntents()
    {
        var stub = new StubGraphClient(new() { ["users('u-1')/mobileAppIntentAndStates('i-1')"] = "mobileAppIntentAndStates" });
        var apps = await new InstallStatusLookup(stub).AppsAsync(Device);
        Assert.Equal(["Google Chrome 153.0.8010.37", "Company Portal", "Contoso VPN 4.2", "Old VPN client"], apps.Select(app => app.Name));
        Assert.Equal(
            [AppInstallOutcome.Available, AppInstallOutcome.Installed, AppInstallOutcome.Failed, AppInstallOutcome.Removed],
            apps.Select(app => app.Outcome));
        Assert.Equal(AppIntent.RequiredAndAvailable, apps[2].Intent);
        Assert.StartsWith("https://graph.microsoft.com/beta/", Assert.Single(stub.Sent).Url.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeviceWithNoUserAsksWithTheEmptyUserId()
    {
        var stub = new StubGraphClient([]);
        Assert.Empty(await new InstallStatusLookup(stub).AppsAsync(Device with { UserId = "" }));
        Assert.Contains($"users('{InstallStatusLookup.NoUser}')", Uri.UnescapeDataString(Assert.Single(stub.Sent).Url.AbsolutePath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailurePicksThisDevicesRowFromTheAppsReport()
    {
        var stub = new StubGraphClient([], request => request.Url.AbsolutePath.EndsWith("retrieveDeviceAppInstallationStatusReport", StringComparison.Ordinal) ? new StubReply("report_app_install_status", null) : null);
        var vpn = new AppInstallStatus { AppId = "app-vpn", Name = "Contoso VPN 4.2", Intent = AppIntent.RequiredInstall, State = AppInstallState.Failed };
        var failure = await new InstallStatusLookup(stub).FailureAsync(vpn, Device);

        Assert.NotNull(failure);
        Assert.Equal("0x87D1041C", failure.HexCode);
        Assert.Equal("The application was not detected after installation completed successfully", failure.Detail);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 11, 4, 37, TimeSpan.Zero), failure.At);
        Assert.StartsWith("0x87D1041C · The app wasn't detected", failure.Summary, StringComparison.Ordinal);

        var request = Assert.Single(stub.Sent);
        Assert.Equal(GraphMethod.Post, request.Method);
        using var body = JsonDocument.Parse(BodyOf(request));
        Assert.Equal("(ApplicationId eq 'app-vpn') and (InstallState eq '2')", body.RootElement.GetProperty("filter").GetString());
        Assert.Equal("CTS-LT-01234", body.RootElement.GetProperty("search").GetString());
        Assert.Equal(1000, body.RootElement.GetProperty("top").GetInt32());

        Assert.Null(await new InstallStatusLookup(stub).FailureAsync(vpn, Device with { Id = "i-404" }));
    }

    [Fact]
    public async Task ConfigurationMergesContextsAndPutsTheWorstFirst()
    {
        var stub = new StubGraphClient([], request => request.Url.AbsolutePath.EndsWith("getConfigurationPoliciesReportForDevice", StringComparison.Ordinal) ? new StubReply("report_configuration_policies", null) : null);
        var policies = await new InstallStatusLookup(stub).ConfigurationAsync(Device);

        Assert.Equal(
            [("LAPS Policy", PolicyDeploymentState.Error), ("Pilot ring", PolicyDeploymentState.Conflict), ("BitlockerPolicy", PolicyDeploymentState.Succeeded)],
            policies.Select(policy => (policy.Name, policy.State)));
        Assert.Equal(["Settings Catalog", "Software Updates", "BitLocker"], policies.Select(policy => policy.TypeName));
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 11, 1, 42, TimeSpan.Zero), policies[0].LastReported);

        var request = Assert.Single(stub.Sent);
        using var body = JsonDocument.Parse(BodyOf(request));
        Assert.False(body.RootElement.TryGetProperty("orderBy", out _));
        Assert.EndsWith("and (IntuneDeviceId eq 'i-1')", body.RootElement.GetProperty("filter").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportTablesAreReadByColumnName()
    {
        var table = ReportTable.Parse(Encoding.UTF8.GetBytes("""
            {"TotalRowCount":7,"Schema":[{"Column":"B","PropertyType":"Int32"},{"Column":"A","PropertyType":"String"},{"Column":"When","PropertyType":"DateTime"}],
             "Values":[[5,"five","2026-10-11T10:44:27"],["6",null,"10/11/2026 11:04:37 AM"]]}
            """));
        Assert.Equal((7, 2), (table.TotalRowCount, table.Count));
        Assert.Equal(("five", 5), (table.Text(0, "a"), table.Int(0, "B")));
        Assert.Equal(("", 6), (table.Text(1, "A"), table.Int(1, "B")));
        Assert.Equal("", table.Text(0, "Missing"));
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 10, 44, 27, TimeSpan.Zero), table.Date(0, "When"));
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 11, 4, 37, TimeSpan.Zero), table.Date(1, "When"));

        Assert.True(Assert.Throws<GraphException>(() => ReportTable.Parse([])).IsEmptyResponse);
        Assert.Equal(GraphErrorKind.Decoding, Assert.Throws<GraphException>(() => ReportTable.Parse("{}"u8.ToArray())).Kind);
    }

    [Fact]
    public async Task OnlyReportActionsAreReadWithPost()
    {
        var stub = new StubGraphClient([]);
        await Assert.ThrowsAsync<ArgumentException>(() => stub.PostReportAsync(GraphRequest.PostJson("deviceManagement/managedDevices/i-1/retire", [])));
        Assert.Empty(stub.Sent);
    }

    [Theory]
    [InlineData(unchecked((int)0x87D1041C), "The app wasn't detected after the installer finished: check the app's detection rule.")]
    [InlineData(unchecked((int)0x80070643), "Windows Installer: fatal error during installation.")]
    [InlineData(unchecked((int)0x80070652), "Windows Installer: another installation was already in progress.")]
    [InlineData(unchecked((int)0x80070005), "Windows error 5.")]
    [InlineData(unchecked((int)0x87D13B9F), null)]
    public void ErrorCodesGetAMeaningOnlyWhenItsCertain(int code, string? meaning)
    {
        Assert.Equal(meaning, InstallErrorText.Meaning(code));
    }

    [Theory]
    [InlineData(AppIntent.Available, AppInstallState.Unknown, AppInstallOutcome.Available)]
    [InlineData(AppIntent.RequiredInstall, AppInstallState.Unknown, AppInstallOutcome.NoStatus)]
    [InlineData(AppIntent.RequiredInstall, AppInstallState.NotInstalled, AppInstallOutcome.Missing)]
    [InlineData(AppIntent.RequiredUninstall, AppInstallState.NotInstalled, AppInstallOutcome.Removed)]
    [InlineData(AppIntent.RequiredUninstall, AppInstallState.Installed, AppInstallOutcome.Missing)]
    [InlineData(AppIntent.Available, AppInstallState.UninstallFailed, AppInstallOutcome.Failed)]
    [InlineData(AppIntent.RequiredInstall, AppInstallState.PendingInstall, AppInstallOutcome.Pending)]
    public void IntentAndStateAddUpToAnOutcome(AppIntent intent, AppInstallState state, AppInstallOutcome outcome)
    {
        Assert.Equal(outcome, AppInstallOutcomes.For(intent, state));
    }

    [Fact]
    public async Task TheTabListsProblemsFirstWithWhyTheyFailed()
    {
        var model = new InstallStatusViewModel(Device, new InstallStatusLookup(new DemoGraphClient(TimeSpan.Zero)), () => { }, new FixedTime(Now));
        await model.LoadAsync();

        Assert.Equal("Contoso VPN 4.2", model.AppList[0].Name);
        Assert.Equal("Failed", model.AppList[0].StateText);
        Assert.StartsWith("0x87D1041C · The app wasn't detected", model.AppList[0].ErrorText, StringComparison.Ordinal);
        Assert.Equal("The application was not detected after installation completed successfully", model.AppList[0].Detail);
        Assert.Equal("Microsoft Teams", model.AppList[1].Name);
        Assert.Equal("1 failed · 1 pending · 3 installed · 1 uninstalled · 1 available · 1 not applicable", model.AppSummary);
        Assert.Equal("Not installed (available)", model.AppList[^2].StateText);
        Assert.Equal(("Contoso Field Tools 2.1", "Not applicable"), (model.AppList[^1].Name, model.AppList[^1].StateText));
        Assert.False(model.HasFailureNote);

        Assert.Equal(("Microsoft Edge settings", "Conflict"), (model.ConfigurationList[0].Name, model.ConfigurationList[0].StateText));
        Assert.Equal("1 conflict · 4 succeeded", model.ConfigurationSummary);
        Assert.Equal("Administrative Templates · reported 5 hours ago", model.ConfigurationList[0].Subtitle.Replace(model.ConfigurationList[0].When, "5 hours ago", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EachSectionFailsOnItsOwnAndErrorDetailsAreOptional()
    {
        var stub = new StubGraphClient(new() { ["users('u-1')/mobileAppIntentAndStates('i-1')"] = "mobileAppIntentAndStates" },
            request => request.Url.AbsolutePath.Contains("/reports/", StringComparison.Ordinal) ? new StubReply(null, GraphException.Forbidden()) : null);
        var model = new InstallStatusViewModel(Device, new InstallStatusLookup(stub), () => { }, new FixedTime(Now));
        await model.LoadAsync();

        Assert.Equal(SectionKind.Loaded, model.Apps.Kind);
        Assert.Equal("Contoso VPN 4.2", model.AppList[0].Name);
        Assert.False(model.AppList[0].HasError);
        Assert.Contains("DeviceManagementApps.Read.All", model.FailureNote, StringComparison.Ordinal);

        Assert.Equal(SectionKind.Failed, model.Configuration.Kind);
        Assert.Equal(GraphFeature.ConfigurationStatus.AccessDeniedMessage(), model.Configuration.Message);
    }
}
