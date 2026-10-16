using System.IO.Compression;
using FieldKit.Core.Demo;
using FieldKit.Core.DeviceLogs;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows (2026-10-16): the device's own Intune Management Extension log, and the theme setting.</summary>
public sealed class DeviceLogTests : IDisposable
{
    private const string App = "4f9c2a7e-1b3d-4c5e-9f60-7a8b9c0d1e2f";
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);
    private readonly string _folder = Directory.CreateTempSubdirectory("fieldkit-logs-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static string FixtureLog() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ime_appworkload.log"));

    private static AppLogReport Analyze(string? text = null) => AppLogAnalyzer.Analyze(ImeLog.Parse(text ?? FixtureLog()), App, "test");

    // ---------- reading the log ----------

    [Fact]
    public void CmTraceLinesParseWithTimeSeverityAndThread()
    {
        var lines = ImeLog.Parse(FixtureLog());
        Assert.Equal(19, lines.Count);
        Assert.Equal(new DateTime(2026, 10, 15, 9, 0, 1, 100), lines[0].At);
        Assert.Equal(("AppWorkload", 1, 21), (lines[0].Component, lines[0].Severity, lines[0].Thread));
        // A message can span lines.
        var download = Assert.Single(lines, line => line.Message.Contains("timed out", StringComparison.Ordinal));
        Assert.Contains("\nDelivery Optimization fell back to HTTP.", download.Message, StringComparison.Ordinal);
        Assert.True(download.IsProblem);
        Assert.Null(ImeLog.Parse("""<![LOG[x]LOG]!><time="25:99:00.000" date="13-40-2026" component="c" context="" type="1" thread="1" file="">""")[0].At);
    }

    [Fact]
    public void TheLastRunGivesTheResultsExitCodeAndErrorCode()
    {
        var log = Analyze();

        Assert.True(log.Found);
        Assert.Equal("Applicable", log.Applicability);
        Assert.Equal(["NotDetected"], log.Detections);
        Assert.Equal(("Install", "Failed"), (log.Execution, log.ExecutionResult));
        // The detection script's lpExitCode 0 came before the installer ran, so it doesn't count.
        Assert.Equal(1603, log.ExitCode);
        Assert.Equal(-2147023293, log.EnforcementErrorCode);
        Assert.StartsWith("[Win32App] Content download retried: the connection timed out", log.DownloadProblem, StringComparison.Ordinal);
        Assert.Equal(["[Win32App][ExecutionActionHandler] Admin did NOT set mapping for lpExitCode: 1603 of app: " + App], log.Errors);
        Assert.Equal(new DateTime(2026, 10, 16, 14, 31, 10, 300), log.LastSeen);
        // The other app's lines on another thread stay out of the excerpt.
        Assert.DoesNotContain(log.Excerpt, line => line.Message.Contains("9a8b7c6d", StringComparison.Ordinal) && !line.Message.Contains(App, StringComparison.Ordinal));
        Assert.Contains(log.Excerpt, line => line.Message.Contains("lpExitCode: 1603", StringComparison.Ordinal));

        Assert.False(AppLogAnalyzer.Analyze(ImeLog.Parse(FixtureLog()), "00000000-1111-2222-3333-444444444444", "test").Found);
        Assert.Equal(("not detected", "requirements not met", ""), (AppLogAnalyzer.Words("NotDetected"), AppLogAnalyzer.Words("RequirementsNotMet"), AppLogAnalyzer.Words("")));
    }

    [Fact]
    public void ALaterPassThatOnlySkippedTheAppKeepsTheResultsBeforeIt()
    {
        const string skipped = $"""
            <![LOG[[Win32App][V3Processor] Processing subgraph with app ids: {App}]LOG]!><time="18:00:00.0000000" date="10-16-2026" component="AppWorkload" context="" type="1" thread="50" file="">
            <![LOG[[Win32App][V3Processor] Reevaluation interval is not expired for subgraph {App} and no override condition applies. Skipping processing.]LOG]!><time="18:00:00.1000000" date="10-16-2026" component="AppWorkload" context="" type="1" thread="50" file="">
            <![LOG[[Win32App][V3Processor] Done processing subgraph.]LOG]!><time="18:00:00.2000000" date="10-16-2026" component="AppWorkload" context="" type="1" thread="50" file="">
            """;
        var log = Analyze(FixtureLog() + skipped);
        Assert.Equal(1603, log.ExitCode);
        Assert.True(log.SkippedSince);
        Assert.Equal(new DateTime(2026, 10, 16, 14, 30, 0), log.RunAt);
        Assert.Equal(new DateTime(2026, 10, 16, 18, 0, 0, 100), log.LastSeen);
        Assert.False(Analyze().SkippedSince);
    }

    [Fact]
    public void TheCurrentAndPreviousLogsAreReadOldestFirst()
    {
        Assert.Equal(
            ["AppWorkload-20260929-140606.log", "AppWorkload.log", "IntuneManagementExtension.log"],
            DeviceLogFiles.Pick(["AppWorkload.log", "AppWorkload-20260911-011353.log", "AppWorkload-20260929-140606.log", "Sensor.log", "IntuneManagementExtension.log", "HealthScripts.log"]));

        // The rolled-over file is older, so the current file's run wins.
        var older = FixtureLog().Replace("lpExitCode: 1603", "lpExitCode: 1618", StringComparison.Ordinal);
        var files = new DeviceLogFiles("test", [new DeviceLogFile("AppWorkload.log", FixtureLog()), new DeviceLogFile("AppWorkload-20260929-140606.log", older)]);
        Assert.Equal(1603, files.Analyze(App).ExitCode);

        // Older extensions logged Win32 apps in IntuneManagementExtension.log.
        var legacy = new DeviceLogFiles("test", [new DeviceLogFile("AppWorkload.log", "nothing"), new DeviceLogFile("IntuneManagementExtension.log", FixtureLog())]);
        Assert.True(legacy.Analyze(App).Found);
    }

    [Fact]
    public async Task LogFilesCopiedFromTheDeviceAreReadAsTheyAre()
    {
        var current = Path.Combine(_folder, "AppWorkload.log");
        var renamed = Path.Combine(_folder, "CTS-LT-01234 AppWorkload copy.txt.log");
        File.WriteAllText(current, FixtureLog());
        File.WriteAllText(renamed, FixtureLog().Replace("lpExitCode: 1603", "lpExitCode: 1618", StringComparison.Ordinal));

        var files = await new DeviceLogReader().FromFilesAsync([current]);
        Assert.Equal(("AppWorkload.log", 1603), (files.Source, files.Analyze(App).ExitCode));

        // A log saved under another name is still read.
        Assert.Equal(1618, (await new DeviceLogReader().FromFilesAsync([renamed])).Analyze(App).ExitCode);

        var missing = Path.Combine(_folder, "gone.log");
        Assert.Equal("Couldn't open gone.log. Check it isn't open in another app, then try again.",
            (await Assert.ThrowsAsync<DeviceLogException>(() => new DeviceLogReader().FromFilesAsync([missing]))).Message);
    }
    [Fact]
    public async Task ADiagnosticsZipIsReadWhereverTheLogsSit()
    {
        var source = Directory.CreateDirectory(Path.Combine(_folder, "zip", "(51) FoldersFiles ProgramData_Microsoft_IntuneManagementExtension_Logs"));
        File.WriteAllText(Path.Combine(source.FullName, "AppWorkload.log"), FixtureLog());
        File.WriteAllText(Path.Combine(_folder, "zip", "msinfo32.log"), "ignored");
        var zip = Path.Combine(_folder, "DiagLogs-CTS-LT-01234.zip");
        ZipFile.CreateFromDirectory(Path.Combine(_folder, "zip"), zip);

        var files = await new DeviceLogReader().FromFilesAsync([zip]);
        Assert.Equal("DiagLogs-CTS-LT-01234.zip", files.Source);
        Assert.Equal(1603, files.Analyze(App).ExitCode);

        var other = Path.Combine(_folder, "other.zip");
        Directory.CreateDirectory(Path.Combine(_folder, "other"));
        File.WriteAllText(Path.Combine(_folder, "other", "readme.txt"), "x");
        ZipFile.CreateFromDirectory(Path.Combine(_folder, "other"), other);
        Assert.StartsWith("other.zip has no Intune Management Extension logs.", (await Assert.ThrowsAsync<DeviceLogException>(() => new DeviceLogReader().FromFilesAsync([other]))).Message, StringComparison.Ordinal);

        var text = Path.Combine(_folder, "not-a-zip.zip");
        File.WriteAllText(text, "hello");
        Assert.Equal("not-a-zip.zip isn't a zip file FieldKit can open.", (await Assert.ThrowsAsync<DeviceLogException>(() => new DeviceLogReader().FromFilesAsync([text]))).Message);
    }

    // ---------- the decoder with the log ----------

    private static AppInstallDiagnosis Diagnose(AppInstallState state, AppLogReport log, int errorCode = 0)
    {
        var app = new AppInstallStatus { AppId = App, Name = "Contoso VPN 4.2", Intent = AppIntent.RequiredInstall, State = state };
        return AppInstallDecoder.Diagnose(new AppInstallEvidence
        {
            App = app,
            Device = new AppDeviceFacts { Name = "CTS-LT-01234", OsVersion = "10.0.26100.4652", LastSync = Now.AddHours(-1) },
            Report = errorCode == 0 ? null : new AppInstallFailure(errorCode, "", null),
            Details = new AppDetails
            {
                Id = App, TypeName = AppTypeNames.Win32,
                Win32 = new Win32AppSettings { Rules = [new AppRule { Kind = AppRuleKind.File, Path = @"C:\Program Files\Contoso\VPN", Name = "vpnclient.exe", Operation = "exists" }] },
            },
            Log = log,
            Now = Now,
        });
    }

    [Fact]
    public void TheLogFillsInWhenIntunesReportHasNoCode()
    {
        // No report code: the log's error code (0x80070643) and its download trouble explain it.
        var failed = Diagnose(AppInstallState.Failed, Analyze());
        Assert.Equal("The installer hit a fatal error", failed.Title);
        Assert.Equal(["The download didn't finish", "The installer failed on the device", "The device's log shows errors"], failed.Causes.Select(cause => cause.Title));
        Assert.DoesNotContain(AppInstallDecoder.ReadLogCheck, failed.WhatToCheck);

        // No code anywhere but the installer's exit code.
        var exitOnly = Diagnose(AppInstallState.Failed, new AppLogReport { Found = true, Execution = "Install", ExecutionResult = "Failed", ExitCode = 1618 });
        Assert.Equal("Another installation was running", exitOnly.Title);
    }

    [Fact]
    public void TheLogConfirmsADetectionFailure()
    {
        var log = new AppLogReport { Found = true, Detections = ["NotDetected", "NotDetected"], Execution = "Install", ExecutionResult = "Success", ExitCode = 0 };
        var diagnosis = Diagnose(AppInstallState.Failed, log, unchecked((int)0x87D1041C));
        Assert.EndsWith("The device's log agrees: the installer exited with 0, then detection didn't find the app.", diagnosis.Causes[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLogSaysWhetherTheDeviceHasStarted()
    {
        var notStarted = Diagnose(AppInstallState.PendingInstall, new AppLogReport { Source = "test" });
        Assert.Equal(("The device hasn't started on it", true), (notStarted.Causes[0].Title, notStarted.Causes[0].Likely));
        Assert.DoesNotContain(notStarted.Causes, cause => cause.Title == "The Intune Management Extension hasn't picked it up yet");

        var detected = Diagnose(AppInstallState.Unknown, new AppLogReport { Found = true, Applicability = "Applicable", Detections = ["Detected"] });
        Assert.Contains(detected.Causes, cause => cause.Title == "The device already finds it installed");

        var lastRun = Diagnose(AppInstallState.NotInstalled, new AppLogReport { Found = true, Applicability = "Applicable", Detections = ["NotDetected"], Execution = "Install", ExitCode = 1618 });
        Assert.Equal("The device's log, last run: applicability applicable; detection not detected; install no result logged (exit code 1618).",
            Assert.Single(lastRun.Causes, cause => cause.Title == "What the device did last").Detail);

        var notApplicable = Diagnose(AppInstallState.NotApplicable, new AppLogReport { Found = true, Applicability = "RequirementsNotMet" });
        Assert.Equal(("The device decided it doesn't apply", "The device's log records the applicability check as “requirements not met”."), (notApplicable.Causes[0].Title, notApplicable.Causes[0].Detail));
    }

    // ---------- the page ----------

    private static async Task<AppInstallDetailViewModel> PageAsync(IntuneDevice device, string entraObjectId)
    {
        var demo = new DemoGraphClient(TimeSpan.Zero);
        var apps = await new InstallStatusLookup(demo).AppsAsync(device);
        var model = new AppInstallDetailViewModel(new AppInstallSubject(device, entraObjectId, apps.Single(app => app.AppId == "app-vpn"), apps),
            new AppInstallLookup(demo), () => { }, new FixedTime(Now), new DemoDeviceLogReader());
        await model.LoadAsync();
        return model;
    }

    [Fact]
    public async Task OpeningTheDevicesLogUpdatesTheDiagnosis()
    {
        var laptop = new IntuneDevice { Id = "i-1", DeviceName = "CTS-LT-01234", OsVersion = "10.0.26100.4652", LastSyncDateTime = Now.AddHours(-1) };
        var model = await PageAsync(laptop, "e-1");
        Assert.True(model.CanReadLogs);
        Assert.False(model.HasLog);
        Assert.Contains(AppInstallDecoder.ReadLogCheck, model.WhatToCheck);
        Assert.Equal("https://intune.microsoft.com/#view/Microsoft_Intune_Devices/DeviceSettingsMenuBlade/~/overview/mdmDeviceId/i-1", model.IntuneDeviceUri.AbsoluteUri);

        await model.ReadLogFilesAsync([@"C:\Users\me\Downloads\AppWorkload.log"]);

        Assert.True(model.HasLog);
        Assert.Contains(new InfoRow("Read from", "AppWorkload.log"), model.LogRows);
        Assert.False(model.HasLogMessage);
        Assert.Contains(new InfoRow("Detection", "not detected, then not detected", InfoTone.Caution), model.LogRows);
        Assert.Contains(new InfoRow("Installer exit code", "0 (success)"), model.LogRows);
        Assert.Contains("The device's log agrees: the installer exited with 0", model.CauseList[0].Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(AppInstallDecoder.ReadLogCheck, model.WhatToCheck);
        Assert.Contains("lpExitCode: 0", model.LogExcerpt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADiagnosticsZipExplainsAnInstallerFailure()
    {
        var desktop = new IntuneDevice { Id = "i-2", DeviceName = "DESKTOP-7Q2LMNP", OsVersion = "10.0.22631.4037", LastSyncDateTime = Now.AddDays(-50) };
        var model = await PageAsync(desktop, "e-2");

        await model.ReadLogFilesAsync([@"C:\Users\me\Downloads\DiagLogs-DESKTOP-7Q2LMNP.zip"]);
        Assert.False(model.HasLogMessage);
        Assert.True(model.HasLog);
        Assert.Contains(new InfoRow("Read from", "DiagLogs-DESKTOP-7Q2LMNP.zip"), model.LogRows);
        Assert.Contains(new InfoRow("Installer exit code", "1603: Windows Installer: fatal error during installation.", InfoTone.Critical), model.LogRows);
        Assert.Equal("The installer hit a fatal error", model.Title);
    }

    // ---------- appearance ----------

    [Fact]
    public void TheThemeIsSavedAndReset()
    {
        using var store = SqliteLocalStore.OpenInMemory();
        var settings = new SettingsStore(store);
        Assert.Equal(AppTheme.System, settings.Theme);
        settings.Theme = AppTheme.Dark;
        Assert.Equal(AppTheme.Dark, new SettingsStore(store).Theme);

        var model = new SettingsViewModel(new AppSession(store, _ => new FakeAuthProvider()), settings, store, "1.5.0");
        Assert.Equal(2, model.ThemeIndex);
        model.ThemeIndex = 1;
        Assert.Equal(AppTheme.Light, settings.Theme);
        model.ThemeIndex = 7;
        Assert.Equal(AppTheme.Light, settings.Theme);

        settings.ResetAll();
        Assert.Equal(AppTheme.System, new SettingsStore(store).Theme);
        store.SetSetting("theme", "purple");
        Assert.Equal(AppTheme.System, new SettingsStore(store).Theme);
    }
}
