using FieldKit.Core.Demo;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows (2026-10-16): "Why didn't it install?" for one app on one device.</summary>
public sealed class AppInstallDecoderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly IntuneDevice Device = new()
    {
        Id = "i-1", DeviceName = "CTS-LT-01234", SerialNumber = "5CG1234XYZ", OperatingSystem = "Windows", OsVersion = "10.0.26100.4652",
        UserId = "u-alex", UserPrincipalName = "alex.doe@contoso.com", UserDisplayName = "Alex Doe", LastSyncDateTime = Now.AddHours(-1),
        Storage = StorageUsage.Create(255013683200, 120259084288),
    };

    private static readonly AppDeviceFacts Facts = AppDeviceFacts.From(Device, new HardwareDetails { ProcessorArchitecture = "x64", PhysicalMemoryBytes = 17179869184 });

    private static AppInstallStatus Status(AppInstallState state, AppIntent intent = AppIntent.RequiredInstall, string id = "app-1", string name = "Contoso VPN 4.2") =>
        new() { AppId = id, Name = name, Intent = intent, State = state };

    private static AppRule FileRule(string folder, string name, string operation = "exists", string op = "notConfigured", string value = "", bool requirement = false) =>
        new() { Kind = AppRuleKind.File, Path = folder, Name = name, Operation = operation, Operator = op, Value = value, IsRequirement = requirement };

    private static AppDetails Win32App(Win32AppSettings? win32 = null, IReadOnlyList<AppRelationship>? relationships = null, IReadOnlyList<AppAssignment>? assignments = null) =>
        new()
        {
            Id = "app-1", Name = "Contoso VPN 4.2", TypeName = AppTypeNames.Win32,
            Assignments = assignments ?? [new AppAssignment(AssignmentTarget.Group("g-laptops"), "required")],
            Relationships = relationships ?? [],
            Win32 = win32 ?? new Win32AppSettings
            {
                Rules = [FileRule(@"C:\Program Files\Contoso\VPN", "vpnclient.exe", "version", "greaterThanOrEqual", "4.2.0")],
                ReturnCodes = [new(0, "success"), new(1707, "success"), new(3010, "softReboot"), new(1641, "hardReboot"), new(1618, "retry")],
                RunAsAccount = "system",
            },
        };

    private static readonly DeviceMembership Laptop = new(
        new Dictionary<string, string> { ["g-laptops"] = "CTS Laptops", ["g-kiosk"] = "Kiosk Devices" },
        new Dictionary<string, string> { ["g-finance"] = "Finance Users" },
        HasUser: true);

    private static AppInstallDiagnosis Diagnose(
        AppInstallStatus app,
        AppDetails? details = null,
        int errorCode = 0,
        string detail = "",
        AppDeviceFacts? facts = null,
        AppTargeting? targeting = null,
        IReadOnlyList<AppInstallStatus>? deviceApps = null) =>
        AppInstallDecoder.Diagnose(new AppInstallEvidence
        {
            App = app,
            Device = facts ?? Facts,
            Report = errorCode == 0 && detail.Length == 0 ? null : new AppInstallFailure(errorCode, detail, Now.AddHours(-3)),
            Details = details,
            Targeting = targeting ?? (details is null ? null : AppTargeting.Resolve(details.Assignments, Laptop, new Dictionary<string, AssignmentFilter>())),
            DeviceApps = deviceApps ?? [],
            Now = Now,
        });

    // ---------- Graph ----------

    private static StubGraphClient AppStub() =>
        new([], request =>
        {
            var path = request.Url.AbsolutePath;
            var query = Uri.UnescapeDataString(request.Url.Query);
            if (path.EndsWith("/mobileApps/app-1", StringComparison.Ordinal))
            {
                return query.Contains("microsoft.graph.win32LobApp/", StringComparison.Ordinal) ? "mobileApp_win32_settings" : "mobileApp_win32";
            }
            if (path.EndsWith("/mobileApps/app-portal", StringComparison.Ordinal)) return "mobileApp_winget";
            if (path.EndsWith("/assignmentFilters/flt-desktops", StringComparison.Ordinal)) return "assignmentFilter";
            return null;
        });

    [Fact]
    public async Task AWin32AppIsReadInTwoBetaRequests()
    {
        var stub = AppStub();
        var app = await new AppInstallLookup(stub).AppAsync("app-1");

        Assert.Equal(2, stub.Sent.Count);
        Assert.All(stub.Sent, request => Assert.StartsWith("https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/app-1?", request.Url.AbsoluteUri, StringComparison.Ordinal));
        var first = Uri.UnescapeDataString(stub.Sent[0].Url.Query);
        Assert.Contains("$select=id,displayName,publisher", first, StringComparison.Ordinal);
        Assert.Contains("$expand=assignments,relationships", first, StringComparison.Ordinal);
        Assert.Contains("microsoft.graph.win32LobApp/rules", Uri.UnescapeDataString(stub.Sent[1].Url.Query), StringComparison.Ordinal);

        Assert.Equal(("Contoso VPN 4.2", "Contoso IT", "Windows app (Win32)"), (app.Name, app.Publisher, app.TypeLabel));
        Assert.Equal(3, app.Assignments.Count);
        Assert.Equal(new AppAssignment(AssignmentTarget.Group("g-laptops"), "required"), app.Assignments[0]);
        Assert.Equal(new AppAssignment(AssignmentTarget.AllDevices, "required", "flt-desktops", AssignmentFilterMode.Include), app.Assignments[1]);
        Assert.Equal(AssignmentTargetKind.ExcludedGroup, app.Assignments[2].Target.Kind);

        Assert.Equal(
            ["Depends on Contoso VPN Driver, which Intune installs first", "Replaces Old VPN client (uninstalls it first)"],
            app.Relationships.Select(relationship => relationship.Description));
        Assert.True(app.Relationships[0].IsDependencyOfThisApp);

        var win32 = Assert.IsType<Win32AppSettings>(app.Win32);
        Assert.Equal(["x64", "arm64"], win32.Architectures);
        Assert.False(win32.ArchitecturesAreLegacy);
        Assert.Equal(("1607", 102, (int?)null), (win32.MinimumWindowsRelease, win32.MinimumFreeDiskSpaceMb, win32.MinimumMemoryMb));
        Assert.Equal(
            [@"C:\Program Files\Contoso\VPN\vpnclient.exe has file version 4.2.0 or later", "A PowerShell script decides. Intune counts the app as installed only when the script exits with 0 and writes something to standard output."],
            win32.DetectionRules.Select(rule => rule.Description));
        Assert.Equal(@"Registry value HKEY_LOCAL_MACHINE\SOFTWARE\Contoso\Site is “HQ” (in the 32-bit registry view on 64-bit Windows)", Assert.Single(win32.RequirementRules).Description);
        Assert.Equal("0 (success), 1707 (success), 3010 (soft restart), 1641 (hard restart), 1618 (retry)", string.Join(", ", win32.ReturnCodes.Select(code => code.Text)));
        Assert.Equal(("system", "ContosoVPN-4.2.0.msi", "msiexec /i \"ContosoVPN-4.2.0.msi\" /qn"), (win32.RunAsAccount, win32.SetupFile, win32.InstallCommand));
    }

    [Fact]
    public async Task OtherAppTypesNeedOneRequest()
    {
        var stub = AppStub();
        var app = await new AppInstallLookup(stub).AppAsync("app-portal");

        Assert.Single(stub.Sent);
        Assert.Null(app.Win32);
        Assert.Equal("WinGet app", app.TypeLabel);
        Assert.Equal(AssignmentTargetKind.AllUsers, Assert.Single(app.Assignments).Target.Kind);
    }

    [Fact]
    public async Task FiltersAndTheReportRowAreReadOnTheirOwn()
    {
        var filter = await new AppInstallLookup(AppStub()).FilterAsync("flt-desktops");
        Assert.Equal(new AssignmentFilter("flt-desktops", "Contoso desktops", "(device.model -startsWith \"ThinkCentre\")", "windows10AndLater"), filter);

        var stub = new StubGraphClient([], request => request.Url.AbsolutePath.EndsWith("retrieveDeviceAppInstallationStatusReport", StringComparison.Ordinal) ? new StubReply("report_app_install_status", null) : null);
        var row = await new AppInstallLookup(stub).ReportAsync("app-vpn", Device);
        Assert.Equal("0x87D1041C", row?.HexCode);
        using var body = System.Text.Json.JsonDocument.Parse(Assert.Single(stub.Sent).Body!);
        Assert.Equal("(ApplicationId eq 'app-vpn')", body.RootElement.GetProperty("filter").GetString());
    }

    // ---------- rules and requirements ----------

    [Fact]
    public void RulesReadAsPlainSentences()
    {
        Assert.Equal(@"C:\Contoso\tag.txt exists (looking where a 32-bit app goes on 64-bit Windows)",
            (FileRule(@"C:\Contoso\", "tag.txt") with { Check32BitOn64System = true }).Description);
        Assert.Equal(@"C:\Contoso\app.exe was modified on or after 2026-01-01",
            FileRule(@"C:\Contoso", "app.exe", "modifiedDate", "greaterThanOrEqual", "2026-01-01").Description);
        Assert.Equal(@"Registry key HKEY_LOCAL_MACHINE\SOFTWARE\Contoso doesn't exist",
            new AppRule { Kind = AppRuleKind.Registry, Path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Contoso", Operation = "doesNotExist" }.Description);
        Assert.Equal(@"Registry value HKLM\SOFTWARE\Contoso\Seats is at least 5",
            new AppRule { Kind = AppRuleKind.Registry, Path = @"HKLM\SOFTWARE\Contoso", Name = "Seats", Operation = "integer", Operator = "greaterThanOrEqual", Value = "5" }.Description);
        Assert.Equal("MSI product {ABC} is installed", new AppRule { Kind = AppRuleKind.ProductCode, ProductCode = "{ABC}", Operator = "notConfigured" }.Description);
        Assert.Equal("MSI product {ABC} is installed, version earlier than 5.0",
            new AppRule { Kind = AppRuleKind.ProductCode, ProductCode = "{ABC}", Operator = "lessThan", Value = "5.0" }.Description);
        Assert.Equal("A PowerShell script's output is version 2.0 or later",
            new AppRule { Kind = AppRuleKind.Script, IsRequirement = true, Operation = "version", Operator = "greaterThanOrEqual", Value = "2.0" }.Description);
    }

    [Fact]
    public void WindowsReleasesAreJudgedOnlyWhenCertain()
    {
        Assert.Equal(14393, WindowsReleases.MinimumBuild("1607"));
        Assert.Equal(22631, WindowsReleases.MinimumBuild("Windows11_23H2"));
        Assert.Null(WindowsReleases.MinimumBuild("21H2"));
        Assert.Equal("Windows 11 23H2", WindowsReleases.DisplayName("Windows11_23H2"));
        Assert.Equal("Windows 11 26H1", WindowsReleases.DisplayName("Windows11_26H1"));
        Assert.Equal("Windows 21H2", WindowsReleases.DisplayName("21H2"));
    }

    [Fact]
    public void RequirementsAreCheckedAgainstWhatIntuneLastHeard()
    {
        var app = new Win32AppSettings
        {
            MinimumWindowsRelease = "Windows11_24H2", Architectures = ["arm64"], MinimumFreeDiskSpaceMb = 500, MinimumMemoryMb = 32768, MinimumProcessors = 4,
        };
        var older = Facts with { OsVersion = "10.0.22631.4037" };
        var checks = AppRequirements.Check(app, older);

        Assert.Equal(
            [("Windows version", false), ("Architecture", false), ("Free disk space", true), ("Memory", false), ("Processors", (bool?)null)],
            checks.Select(check => (check.Label, check.Met)));
        Assert.Equal(("Windows 11 24H2 or later", "build 22631"), (checks[0].Needs, checks[0].DeviceHas));
        Assert.Equal(("ARM64", "x64"), (checks[1].Needs, checks[1].DeviceHas));
        Assert.Equal(AppRequirements.NotReported, checks[4].DeviceHas);

        // The older setting predates ARM64, so an ARM64 device isn't judged against it; unknown devices never are.
        var legacy = new Win32AppSettings { Architectures = ["x64"], ArchitecturesAreLegacy = true };
        Assert.Null(AppRequirements.Check(legacy, Facts with { Architecture = "arm64" })[0].Met);
        Assert.False(AppRequirements.Check(legacy, Facts with { Architecture = "x86" })[0].Met);
        Assert.Null(AppRequirements.Check(app, Facts with { Architecture = null, FreeBytes = null })[1].Met);
    }

    [Fact]
    public void TargetingFollowsTheDeviceAndUserGroupsAndCarriesFilters()
    {
        AppAssignment[] assignments =
        [
            new(AssignmentTarget.Group("g-laptops"), "required"),
            new(AssignmentTarget.Group("g-finance"), "available"),
            new(AssignmentTarget.AllUsers, "required", "flt-desktops", AssignmentFilterMode.Exclude),
            new(AssignmentTarget.Group("g-other"), "required"),
            new(AssignmentTarget.ExcludedGroup("g-kiosk"), "required"),
        ];
        var filters = new Dictionary<string, AssignmentFilter> { ["flt-desktops"] = new("flt-desktops", "Contoso desktops", "(device.model -startsWith \"ThinkCentre\")", "windows10AndLater") };
        var targeting = AppTargeting.Resolve(assignments, Laptop, filters);

        Assert.Equal(
            [("CTS Laptops (device group)", false, "Required"), ("Finance Users (user group)", true, "Available"), ("All users", true, "Required")],
            targeting.Includes.Select(match => (match.Via, match.ThroughUser, match.IntentTitle)));
        Assert.Equal("Except devices that match “Contoso desktops”: (device.model -startsWith \"ThinkCentre\")", targeting.Includes[2].FilterText);
        Assert.Equal(("Kiosk Devices (device group)", "Excluded"), (Assert.Single(targeting.Excludes).Via, targeting.Excludes[0].IntentTitle));

        var noUser = AppTargeting.Resolve(assignments, Laptop with { HasUser = false }, filters);
        Assert.DoesNotContain(noUser.Includes, match => match.Via == "All users");
    }

    // ---------- the decoder ----------

    [Fact]
    public void NotDetectedPointsAtTheDetectionRule()
    {
        var diagnosis = Diagnose(Status(AppInstallState.Failed), Win32App(), unchecked((int)0x87D1041C));

        Assert.Equal((AppInstallVerdict.Failed, "Installed, but not detected"), (diagnosis.Verdict, diagnosis.Title));
        var cause = diagnosis.Causes[0];
        Assert.True(cause.Likely);
        Assert.Equal("The detection rule doesn't match what was installed", cause.Title);
        Assert.Contains(@"Intune looks for this: C:\Program Files\Contoso\VPN\vpnclient.exe has file version 4.2.0 or later.", cause.Detail, StringComparison.Ordinal);
        Assert.Contains(AppInstallDecoder.ReadLogCheck, diagnosis.WhatToCheck);
        Assert.Single(diagnosis.Causes);
    }

    [Fact]
    public void APerUserInstallWithAMachineWideRuleIsCalledOut()
    {
        var app = Win32App(new Win32AppSettings { Rules = [FileRule(@"C:\Program Files\Contoso\VPN", "vpnclient.exe")], RunAsAccount = "user" });
        var diagnosis = Diagnose(Status(AppInstallState.Failed), app, unchecked((int)0x87D1041C));
        Assert.Equal(["The detection rule doesn't match what was installed", "It installs for the user, but detection looks machine-wide"], diagnosis.Causes.Select(cause => cause.Title));

        var script = Win32App(new Win32AppSettings { Rules = [new AppRule { Kind = AppRuleKind.Script }] });
        Assert.Equal("The detection script didn't report the app", Diagnose(Status(AppInstallState.Failed), script, unchecked((int)0x87D1041C)).Causes[0].Title);
    }

    [Theory]
    [InlineData(0x80070643u, "The installer hit a fatal error", "The installer failed on the device")]
    [InlineData(0x80070652u, "Another installation was running", "Windows Installer was busy")]
    [InlineData(0x80070642u, "The installation was cancelled", "The installer was waiting for someone")]
    [InlineData(0x80070659u, "Blocked by policy", "Blocked by policy")]
    [InlineData(0x80070666u, "Another version is already installed", "Another version is already installed")]
    [InlineData(0x80070653u, "The installer package couldn't be opened", "The installer package couldn't be opened")]
    [InlineData(0x80070005u, "The installer exited with 5", "The installer exited with 5")]
    public void InstallerExitCodesAreExplained(uint code, string title, string cause)
    {
        var diagnosis = Diagnose(Status(AppInstallState.Failed), Win32App(), unchecked((int)code));
        Assert.Equal((title, cause), (diagnosis.Title, diagnosis.Causes[0].Title));
    }

    [Fact]
    public void ReturnCodesChangeWhatAnExitCodeMeans()
    {
        // 1618 marked Retry clears on its own; without it, add it.
        var retried = Diagnose(Status(AppInstallState.Failed), Win32App(), unchecked((int)0x80070652));
        Assert.Contains("usually clears on its own", retried.Causes[0].Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Add 1618 to the app's return codes as Retry.", retried.WhatToCheck);
        var bare = Win32App(new Win32AppSettings { ReturnCodes = [new(0, "success")] });
        Assert.Contains("Add 1618 to the app's return codes as Retry.", Diagnose(Status(AppInstallState.Failed), bare, unchecked((int)0x80070652)).WhatToCheck);

        // 3010 isn't a failure when it's mapped to a restart; unmapped, it is.
        Assert.Equal("The installer asked for a restart", Diagnose(Status(AppInstallState.Failed), bare, unchecked((int)0x80070BC2)).Title);
        Assert.Equal("The installer exited with 3010", Diagnose(Status(AppInstallState.Failed), Win32App(), unchecked((int)0x80070BC2)).Title);

        var unknown = Diagnose(Status(AppInstallState.Failed), Win32App(), unchecked((int)0x80070005));
        Assert.Contains("It isn't one of the app's return codes", unknown.Causes[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherCodesShowIntunesOwnWords()
    {
        var diagnosis = Diagnose(Status(AppInstallState.Failed), Win32App(), unchecked((int)0x87D13B9F), "The app couldn't be downloaded.");
        Assert.Equal(("Install failed", "Intune error 0x87D13B9F"), (diagnosis.Title, diagnosis.Causes[0].Title));
        Assert.Equal("Intune's description: “The app couldn't be downloaded”.", diagnosis.Causes[0].Detail);

        var noReport = Diagnose(Status(AppInstallState.Failed), Win32App());
        Assert.Contains("couldn't read the error code", noReport.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedDependencyComesFirstAndAReplacedAppIsWorthRulingOut()
    {
        AppRelationship[] relationships =
        [
            new() { TargetId = "app-2", TargetName = "Contoso VPN Driver", Kind = AppRelationshipKind.Dependency, TargetIsChild = true, AutoInstall = true },
            new() { TargetId = "app-3", TargetName = "Old VPN client", Kind = AppRelationshipKind.Supersedence, TargetIsChild = true, Replaces = true },
        ];
        var diagnosis = Diagnose(Status(AppInstallState.Failed), Win32App(relationships: relationships), unchecked((int)0x80070643),
            deviceApps: [Status(AppInstallState.Failed, id: "app-2", name: "Contoso VPN Driver")]);

        Assert.Equal(
            [("Its dependency Contoso VPN Driver failed", true), ("The installer failed on the device", true), ("It replaces Old VPN client", false)],
            diagnosis.Causes.Select(cause => (cause.Title, cause.Likely)));
    }

    [Fact]
    public void NotApplicableNamesTheRequirementThatFailed()
    {
        var app = Win32App(new Win32AppSettings
        {
            Architectures = ["arm64"],
            Rules = [FileRule(@"C:\ProgramData\Contoso", "field-device.tag", requirement: true)],
        });
        var diagnosis = Diagnose(Status(AppInstallState.NotApplicable), app);

        Assert.Equal((AppInstallVerdict.NotApplicable, "Doesn't apply to this device"), (diagnosis.Verdict, diagnosis.Title));
        Assert.Equal([("Doesn't meet the architecture requirement", true), ("A requirement rule isn't met", false)], diagnosis.Causes.Select(cause => (cause.Title, cause.Likely)));
        Assert.Equal("The app needs ARM64; this device has x64.", diagnosis.Causes[0].Detail);
    }

    [Fact]
    public void AFilterIsLikelyOnlyWhenEveryPathGoesThroughOne()
    {
        var filtered = Win32App(new Win32AppSettings(), assignments: [new AppAssignment(AssignmentTarget.AllDevices, "required", "flt-desktops", AssignmentFilterMode.Include)]);
        var only = Diagnose(Status(AppInstallState.NotApplicable), filtered);
        Assert.Equal(("An assignment filter", true), (only.Causes[0].Title, only.Causes[0].Likely));
        Assert.Contains("In Intune, open the device's Filter evaluation report to see whether each filter matched.", only.WhatToCheck);

        var mixed = Win32App(new Win32AppSettings(), assignments:
        [
            new AppAssignment(AssignmentTarget.AllDevices, "required", "flt-desktops", AssignmentFilterMode.Include),
            new AppAssignment(AssignmentTarget.Group("g-laptops"), "required"),
        ]);
        Assert.False(Diagnose(Status(AppInstallState.NotApplicable), mixed).Causes[0].Likely);

        var nothing = Diagnose(Status(AppInstallState.NotApplicable), Win32App(new Win32AppSettings()));
        Assert.Equal("Nothing FieldKit can check failed", Assert.Single(nothing.Causes).Title);
        var store = Diagnose(Status(AppInstallState.NotApplicable), Win32App() with { TypeName = "winGetApp", Win32 = null });
        Assert.Equal("Made for a different device", Assert.Single(store.Causes).Title);
    }

    [Fact]
    public void WaitingLooksAtTheCheckInTheUserAndTheDependencies()
    {
        var silent = Facts with { LastSync = Now.AddDays(-10) };
        var teams = Win32App(relationships:
            [new AppRelationship { TargetId = "app-webview2", TargetName = "Microsoft Edge WebView2 Runtime", Kind = AppRelationshipKind.Dependency, TargetIsChild = true, AutoInstall = true }],
            assignments: [new AppAssignment(AssignmentTarget.AllUsers, "required")]);
        var diagnosis = Diagnose(Status(AppInstallState.PendingInstall, name: "Microsoft Teams"), teams, facts: silent);

        Assert.Equal((AppInstallVerdict.Waiting, "Waiting to install"), (diagnosis.Verdict, diagnosis.Title));
        Assert.Equal(
            [
                ("Hasn't checked in for 10 days", true),
                ("Depends on Microsoft Edge WebView2 Runtime", false),
                ("It's assigned to the user, not the device", false),
                ("The Intune Management Extension hasn't picked it up yet", false),
            ],
            diagnosis.Causes.Select(cause => (cause.Title, cause.Likely)));
        Assert.Contains("once Alex Doe signs in on this device", diagnosis.Causes[2].Detail, StringComparison.Ordinal);
        // A quiet device gets "wake it, then sync" instead of the plain sync advice.
        Assert.Equal(AppInstallDecoder.WakeAndSyncCheck, diagnosis.WhatToCheck[0]);
        Assert.DoesNotContain(AppInstallDecoder.SyncCheck, diagnosis.WhatToCheck);
        Assert.Contains(AppInstallDecoder.SyncCheck, Diagnose(Status(AppInstallState.PendingInstall), Win32App()).WhatToCheck);

        var recent = Diagnose(Status(AppInstallState.PendingInstall), Win32App(), facts: Facts with { LastSync = Now.AddHours(-30) });
        Assert.Equal(("Hasn't checked in for 30 hours", false), (recent.Causes[0].Title, recent.Causes[0].Likely));
    }

    [Fact]
    public void NoStatusWithNoMatchingAssignmentSaysSo()
    {
        var elsewhere = Win32App(assignments: [new AppAssignment(AssignmentTarget.Group("g-other"), "required")]);
        var diagnosis = Diagnose(Status(AppInstallState.Unknown), elsewhere);

        Assert.Equal("No status from the device yet", diagnosis.Title);
        Assert.Equal("FieldKit can't see which assignment reaches this device", diagnosis.Causes[0].Title);
    }

    [Fact]
    public void ConflictsAndExclusionsAreWorthRulingOut()
    {
        var app = Win32App(assignments:
        [
            new AppAssignment(AssignmentTarget.Group("g-laptops"), "required"),
            new AppAssignment(AssignmentTarget.Group("g-finance"), "uninstall"),
            new AppAssignment(AssignmentTarget.ExcludedGroup("g-kiosk"), "required"),
        ]);
        var titles = Diagnose(Status(AppInstallState.NotInstalled), app).Causes.Select(cause => cause.Title).ToList();
        Assert.Contains("Excluded through Kiosk Devices (device group)", titles);
        Assert.Contains("It's assigned to both install and uninstall", titles);
    }

    [Fact]
    public void SettledStatesNeedNoCauses()
    {
        Assert.Equal((AppInstallVerdict.Installed, 0), Verdict(Diagnose(Status(AppInstallState.Installed), Win32App())));
        Assert.Equal((AppInstallVerdict.Available, 0), Verdict(Diagnose(Status(AppInstallState.Unknown, AppIntent.Available))));
        Assert.Equal((AppInstallVerdict.Uninstalled, 0), Verdict(Diagnose(Status(AppInstallState.NotInstalled, AppIntent.RequiredUninstall))));

        var still = Diagnose(Status(AppInstallState.Installed, AppIntent.RequiredUninstall), Win32App());
        Assert.Equal(("Still installed", "The uninstall hasn't run, or didn't remove it"), (still.Title, still.Causes[0].Title));

        static (AppInstallVerdict, int) Verdict(AppInstallDiagnosis diagnosis) => (diagnosis.Verdict, diagnosis.Causes.Count);
    }

    // ---------- the page, against demo data ----------

    private static async Task<AppInstallDetailViewModel> PageAsync(string appId, IntuneDevice? device = null, string? entraObjectId = "e-1", IGraphClient? client = null)
    {
        var demo = client ?? new DemoGraphClient(TimeSpan.Zero);
        var target = device ?? Device;
        var apps = await new InstallStatusLookup(demo).AppsAsync(target);
        return new AppInstallDetailViewModel(new AppInstallSubject(target, entraObjectId, apps.Single(app => app.AppId == appId), apps),
            new AppInstallLookup(demo), () => { }, new FixedTime(Now));
    }

    [Fact]
    public async Task ThePageExplainsTheDemoVpnFailure()
    {
        var model = await PageAsync("app-vpn");
        Assert.True(model.IsWorking);
        await model.LoadAsync();

        Assert.False(model.IsWorking);
        Assert.Equal(("Failed", "Installed, but not detected", InfoTone.Critical), (model.VerdictLabel, model.Title, model.VerdictTone));
        Assert.Equal("Contoso VPN 4.2 · CTS-LT-01234 · Required", model.Subtitle);
        Assert.Equal("The detection rule doesn't match what was installed", model.CauseList[0].Title);
        Assert.Equal(("1.", "Likely"), (model.CauseList[0].NumberText, model.CauseList[0].Badge));
        Assert.Equal("Likely causes, most likely first", model.CausesHeading);
        Assert.Contains(model.ResultRows, row => row.Label == "Error code" && row.Value.StartsWith("0x87D1041C", StringComparison.Ordinal));

        Assert.Equal(
            [("Required", "CTS Laptops (device group)"), ("Required", "All devices")],
            model.TargetList.Select(item => (item.Intent, item.Via)));
        Assert.Equal("Only devices that match “Contoso desktops”: (device.model -startsWith \"ThinkCentre\")", model.TargetList[1].FilterText);
        Assert.Contains("Filter evaluation", model.TargetingNote, StringComparison.Ordinal);

        Assert.Equal(
            [("Windows version", InfoTone.Good), ("Architecture", InfoTone.Good), ("Free disk space", InfoTone.Good)],
            model.RequirementRows.Select(row => (row.Label, row.Tone)));
        Assert.Equal([@"C:\Program Files\Contoso\VPN\vpnclient.exe has file version 4.2.0 or later"], model.DetectionList);
        Assert.Contains(new InfoRow("Installs as", "System"), model.PackageRows);
        Assert.Equal("msiexec /i \"ContosoVPN-4.2.0.msi\" /qn", model.InstallCommand);
        Assert.False(model.HasRelated);
    }

    [Fact]
    public async Task ThePageCoversNotApplicableWaitingAndAnInstallerError()
    {
        var tools = await PageAsync("app-fieldtools");
        await tools.LoadAsync();
        Assert.Equal(("Not applicable", "Doesn't meet the architecture requirement"), (tools.VerdictLabel, tools.CauseList[0].Title));
        Assert.Contains(tools.RequirementRows, row => row is { Label: "Architecture", Tone: InfoTone.Critical });

        var teams = await PageAsync("app-teams");
        await teams.LoadAsync();
        Assert.Equal("Waiting to install", teams.Title);
        var related = Assert.Single(teams.RelatedList);
        Assert.Equal(("Depends on Microsoft Edge WebView2 Runtime, which Intune installs first", "Not assigned to this device"), (related.Description, related.StateText));
        Assert.Contains(teams.CauseList, cause => cause.Title == "It's assigned to the user, not the device");
        Assert.Equal("Worth ruling out", teams.CausesHeading);

        var desktop = new IntuneDevice { Id = "i-2", DeviceName = "DESKTOP-7Q2LMNP", OsVersion = "10.0.22631.4037", LastSyncDateTime = Now.AddDays(-50) };
        var vpn = await PageAsync("app-vpn", desktop, entraObjectId: "e-2");
        await vpn.LoadAsync();
        Assert.Equal("The installer hit a fatal error", vpn.Title);
        Assert.Equal([("Required", "All devices")], vpn.TargetList.Select(item => (item.Intent, item.Via)));

        var reader = await PageAsync("app-reader");
        await reader.LoadAsync();
        Assert.Equal("Available in Company Portal", reader.Title);

        var portal = await PageAsync("app-portal");
        await portal.LoadAsync();
        Assert.Equal(SectionKind.Skipped, portal.SetupState.Kind);
        Assert.Contains("This is a WinGet app", portal.SetupState.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachReadFailsOnItsOwn()
    {
        var demo = new DemoGraphClient(TimeSpan.Zero);
        var failing = new ScriptedGraphClient(request =>
        {
            var path = request.Url.AbsolutePath;
            if (path.Contains("/transitiveMemberOf", StringComparison.Ordinal)) throw GraphException.Forbidden();
            if (path.Contains("/reports/", StringComparison.Ordinal)) throw GraphException.Forbidden();
            return System.Text.Encoding.UTF8.GetString(demo.SendAsync(request).GetAwaiter().GetResult());
        });
        var apps = await new InstallStatusLookup(demo).AppsAsync(Device);
        var vpn = apps.Single(app => app.AppId == "app-vpn") with { Failure = new AppInstallFailure(unchecked((int)0x87D1041C), "", null) };
        var model = new AppInstallDetailViewModel(new AppInstallSubject(Device, "e-1", vpn, apps), new AppInstallLookup(failing), () => { }, new FixedTime(Now));
        await model.LoadAsync();

        Assert.Equal(SectionKind.Failed, model.Targeting.Kind);
        Assert.Equal(GraphFeature.Groups.AccessDeniedMessage(), model.Targeting.Message);
        Assert.Contains(model.ResultRows, row => row.Label == "Install report" && row.Value.Contains("DeviceManagementApps.Read.All", StringComparison.Ordinal));
        // The tab's error code stands in for the report, so the diagnosis still names the rule.
        Assert.Equal("Installed, but not detected", model.Title);
        Assert.Equal(SectionKind.Loaded, model.SetupState.Kind);
    }

    [Fact]
    public async Task EveryDemoAppHasDetails()
    {
        var demo = new DemoGraphClient(TimeSpan.Zero);
        foreach (var app in await new InstallStatusLookup(demo).AppsAsync(Device))
        {
            var details = await new AppInstallLookup(demo).AppAsync(app.AppId);
            Assert.Equal(app.Name, details.Name);
        }
    }
}
