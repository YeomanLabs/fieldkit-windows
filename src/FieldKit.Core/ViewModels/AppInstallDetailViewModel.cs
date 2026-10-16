using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// "Why didn't it install?" for one app on one device (Windows only, 2026-10-16): the verdict and
/// likely causes, the install result, how the app is assigned here, its requirements and
/// detection rules, and related apps. Each read fails on its own; the diagnosis uses whatever
/// came back.
/// </summary>
public sealed partial class AppInstallDetailViewModel : ObservableObject
{
    private readonly AppInstallLookup _lookup;
    private readonly IDeviceLogReader? _logs;
    private readonly Action _onUnauthorized;
    private readonly TimeProvider _time;
    private CancellationTokenSource _cancellation = new();
    private CancellationTokenSource _logCancellation = new();
    private bool _loaded;

    /// <param name="logs">Reads the device's own log; null leaves the Device log card out.</param>
    public AppInstallDetailViewModel(AppInstallSubject subject, AppInstallLookup lookup, Action onUnauthorized, TimeProvider? time = null, IDeviceLogReader? logs = null)
    {
        Subject = subject;
        _lookup = lookup;
        _logs = logs;
        _onUnauthorized = onUnauthorized;
        _time = time ?? TimeProvider.System;
        Details = SectionState<AppDetails>.Loading;
        Targeting = SectionState<AppTargeting>.Loading;
        Facts = AppDeviceFacts.From(subject.Device, null);
    }

    public AppInstallSubject Subject { get; }

    public AppInstallStatus App => Subject.App;

    public IntuneDevice Device => Subject.Device;

    // ---------- the verdict ----------

    /// <summary>Null while FieldKit is still reading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorking), nameof(Title), nameof(VerdictLabel), nameof(VerdictTone), nameof(Explanation),
        nameof(CauseList), nameof(HasCauses), nameof(CausesHeading), nameof(WhatToCheck), nameof(HasChecks))]
    public partial AppInstallDiagnosis? Diagnosis { get; private set; }

    public bool IsWorking => Diagnosis is null;

    public string Title => Diagnosis?.Title ?? App.Name;

    public string VerdictLabel => Diagnosis?.Label ?? App.Outcome.DisplayName(App.Intent);

    public InfoTone VerdictTone => (Diagnosis?.Verdict) switch
    {
        AppInstallVerdict.Failed => InfoTone.Critical,
        AppInstallVerdict.NotApplicable or AppInstallVerdict.NotInstalled or AppInstallVerdict.Waiting => InfoTone.Caution,
        AppInstallVerdict.Installed or AppInstallVerdict.Uninstalled => InfoTone.Good,
        _ => InfoTone.Normal,
    };

    /// <summary>"Contoso VPN 4.2 · CTS-LT-01234 · Required".</summary>
    public string Subtitle => DeviceText.Join(" · ", App.Name, DeviceText.Name(Device), App.Intent.DisplayName());

    public string Explanation => Diagnosis?.Explanation ?? "";

    public IReadOnlyList<AppCauseItem> CauseList => Diagnosis?.Causes.Select((cause, index) => new AppCauseItem(index + 1, cause)).ToList() ?? [];

    public bool HasCauses => CauseList.Count > 0;

    /// <summary>Only promises likely causes when there are some.</summary>
    public string CausesHeading => Diagnosis?.Causes.Any(cause => cause.Likely) == true ? "Likely causes, most likely first" : "Worth ruling out";

    public IReadOnlyList<string> WhatToCheck => Diagnosis?.WhatToCheck ?? [];

    public bool HasChecks => WhatToCheck.Count > 0;

    // ---------- install result ----------

    /// <summary>The device's row in Intune's install report; null when there's none or it couldn't be read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultRows))]
    public partial AppInstallFailure? Report { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultRows))]
    public partial string? ReportNote { get; private set; }

    public IReadOnlyList<InfoRow> ResultRows
    {
        get
        {
            var now = _time.GetUtcNow();
            var report = Report ?? App.Failure;
            var rows = new List<InfoRow>
            {
                new("Status", App.Outcome.DisplayName(App.Intent), AppStatusItem.From(App, now).Tone),
                new("Assigned as", App.Intent.DisplayName()),
            };
            if (App.Version.Length > 0 && !App.Name.Contains(App.Version, StringComparison.Ordinal)) rows.Add(new("Version", App.Version));
            if (report is { ErrorCode: not 0 } failure) rows.Add(new("Error code", failure.Summary, InfoTone.Critical));
            if (report is { Detail.Length: > 0 }) rows.Add(new("Intune's description", report.Detail));
            if (report?.At is { } at) rows.Add(new("Reported", DateText.RelativeAndAbsolute(at, now)));
            if (ReportNote is { } note) rows.Add(new("Install report", note, InfoTone.Caution));
            var stale = Device.LastSyncDateTime is not { } synced || now - synced >= AppInstallDecoder.ShortSilence;
            rows.Add(new("Device last checked in", DateText.RelativeAndAbsolute(Device.LastSyncDateTime, now), stale ? InfoTone.Caution : InfoTone.Normal));
            return rows;
        }
    }

    // ---------- assignment ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetList), nameof(HasTargets), nameof(ExclusionList), nameof(HasExclusions), nameof(TargetingNote), nameof(HasTargetingNote))]
    public partial SectionState<AppTargeting> Targeting { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetingNote), nameof(HasTargetingNote))]
    public partial string? FilterNote { get; private set; }

    public IReadOnlyList<AppTargetItem> TargetList => Targeting.Value?.Includes.Select(match => new AppTargetItem(match)).ToList() ?? [];

    public bool HasTargets => TargetList.Count > 0;

    public IReadOnlyList<AppTargetItem> ExclusionList => Targeting.Value?.Excludes.Select(match => new AppTargetItem(match)).ToList() ?? [];

    public bool HasExclusions => ExclusionList.Count > 0;

    public string TargetingNote
    {
        get
        {
            if (Targeting.Value is not { } targeting) return "";
            var notes = new List<string>();
            if (targeting.Includes.Count == 0)
            {
                notes.Add("None of the app's assignments match this device's groups or its primary user's. Intune may be targeting another user who signs in on it.");
            }
            if (targeting.Includes.Any(match => match.Assignment.HasFilter))
            {
                notes.Add("FieldKit can't evaluate assignment filters: the device's Filter evaluation report in Intune shows whether each one matched.");
            }
            if (FilterNote is { } note) notes.Add(note);
            return string.Join(" ", notes);
        }
    }

    public bool HasTargetingNote => TargetingNote.Length > 0;

    // ---------- how the app is set up ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SetupState), nameof(RequirementRows), nameof(HasRequirements), nameof(DetectionList), nameof(HasDetection),
        nameof(PackageRows), nameof(InstallCommand), nameof(HasInstallCommand), nameof(UninstallCommand), nameof(HasUninstallCommand),
        nameof(RelatedList), nameof(HasRelated))]
    public partial SectionState<AppDetails> Details { get; private set; }

    /// <summary>What FieldKit knows about the device; architecture and memory arrive with the hardware read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequirementRows))]
    public partial AppDeviceFacts Facts { get; private set; }

    /// <summary>The requirements and detection card: skipped for apps that aren't Win32.</summary>
    public SectionState<AppDetails> SetupState =>
        Details.Value is { Win32: null } app
            ? SectionState<AppDetails>.Skipped($"FieldKit reads requirements and detection rules for Win32 apps. This is a {app.TypeLabel}, which Intune installs its own way.")
            : Details;

    private Win32AppSettings? Win32 => Details.Value?.Win32;

    public IReadOnlyList<InfoRow> RequirementRows
    {
        get
        {
            if (Win32 is not { } win32) return [];
            var rows = AppRequirements.Check(win32, Facts)
                .Select(check => new InfoRow(check.Label, $"Needs {check.Needs}. This device: {check.DeviceHas}.",
                    check.Met switch { true => InfoTone.Good, false => InfoTone.Critical, _ => InfoTone.Normal }))
                .ToList();
            rows.AddRange(win32.RequirementRules.Select(rule => new InfoRow("Requirement rule", $"{rule.Description}. Runs on the device; FieldKit can't check it.")));
            return rows;
        }
    }

    public bool HasRequirements => RequirementRows.Count > 0;

    public IReadOnlyList<string> DetectionList => Win32?.DetectionRules.Select(rule => rule.Description).ToList() ?? [];

    public bool HasDetection => DetectionList.Count > 0;

    public IReadOnlyList<InfoRow> PackageRows
    {
        get
        {
            if (Details.Value is not { } app) return [];
            var rows = new List<InfoRow> { new("Type", app.TypeLabel) };
            if (app.Publisher.Length > 0) rows.Add(new("Publisher", app.Publisher));
            if (app.Win32 is { } win32)
            {
                if (win32.RunAsAccount.Length > 0) rows.Add(new("Installs as", win32.InstallsAsUser ? "The signed-in user" : "System"));
                if (win32.SetupFile.Length > 0) rows.Add(new("Setup file", win32.SetupFile));
                if (win32.ReturnCodes.Count > 0) rows.Add(new("Return codes", string.Join(", ", win32.ReturnCodes.Select(code => code.Text))));
            }
            return rows;
        }
    }

    public string InstallCommand => Win32?.InstallCommand ?? "";

    public bool HasInstallCommand => InstallCommand.Length > 0;

    public string UninstallCommand => Win32?.UninstallCommand ?? "";

    public bool HasUninstallCommand => UninstallCommand.Length > 0;

    public IReadOnlyList<RelatedAppItem> RelatedList =>
        Details.Value?.Relationships
            .Select(relationship => new RelatedAppItem(relationship,
                Subject.DeviceApps.FirstOrDefault(app => app.AppId.Equals(relationship.TargetId, StringComparison.OrdinalIgnoreCase))))
            .ToList() ?? [];

    public bool HasRelated => RelatedList.Count > 0;

    // ---------- the device's own log ----------

    public bool CanReadLogs => _logs is not null;

    /// <summary>What the Intune Management Extension's log says about the app; null until it's been read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLog), nameof(LogRows), nameof(LogExcerpt), nameof(HasLogExcerpt), nameof(LogExcerptHeader))]
    public partial AppLogReport? Log { get; private set; }

    public bool HasLog => Log is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotReadingLog))]
    public partial bool IsReadingLog { get; private set; }

    public bool IsNotReadingLog => !IsReadingLog;

    /// <summary>Why the log couldn't be read, or what's being tried.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogMessage))]
    public partial string? LogMessage { get; private set; }

    public bool HasLogMessage => LogMessage is not null;

    public string LogIntro =>
        $@"See what the Intune Management Extension on {DeviceText.Name(Device)} actually did with this app, from its own log. In Intune, run Collect diagnostics on the device, download the zip when it's ready, then open it here. AppWorkload.log copied from the device's C:\ProgramData\Microsoft\IntuneManagementExtension\Logs works too. FieldKit only reads the file; nothing is changed or kept.";

    /// <summary>The device in the Intune admin center, where Collect diagnostics is.</summary>
    public Uri IntuneDeviceUri => new($"https://intune.microsoft.com/#view/Microsoft_Intune_Devices/DeviceSettingsMenuBlade/~/overview/mdmDeviceId/{Uri.EscapeDataString(Device.Id)}");

    public IReadOnlyList<InfoRow> LogRows
    {
        get
        {
            if (Log is not { } log) return [];
            var rows = new List<InfoRow> { new("Read from", log.Source) };
            if (!log.Found)
            {
                rows.Add(new("This app", "Not mentioned anywhere in the log", InfoTone.Caution));
                return rows;
            }
            if (log.LastSeen is { } seen) rows.Add(new("Last mentioned", DateText.DeviceClock(seen)));
            if (log.RunAt is { } run && (log.SkippedSince || log.LastSeen is not { } last || last - run > TimeSpan.FromMinutes(5)))
            {
                rows.Add(new("Results below from", DateText.DeviceClock(run)));
            }
            if (log.SkippedSince) rows.Add(new("Since then", "Passed over: not due to be checked again yet"));
            if (log.Applicability.Length > 0)
            {
                var applies = log.Applicability.Equals("Applicable", StringComparison.OrdinalIgnoreCase);
                rows.Add(new("Applicability", AppLogAnalyzer.Words(log.Applicability), applies ? InfoTone.Normal : InfoTone.Critical));
            }
            if (log.Detections.Count > 0)
            {
                var found = log.LastDetection.Equals("Detected", StringComparison.OrdinalIgnoreCase);
                rows.Add(new("Detection", string.Join(", then ", log.Detections.Select(AppLogAnalyzer.Words)), found ? InfoTone.Good : InfoTone.Caution));
            }
            if (log.Execution.Length > 0)
            {
                var failed = log.ExecutionResult.Equals("Failed", StringComparison.OrdinalIgnoreCase);
                var result = log.ExecutionResult.Length > 0 ? AppLogAnalyzer.Words(log.ExecutionResult) : "no result logged";
                var which = log.Execution.Equals("Uninstall", StringComparison.OrdinalIgnoreCase) ? "Uninstaller" : "Installer";
                rows.Add(new(which, $"ran, result: {result}", failed ? InfoTone.Critical : InfoTone.Normal));
            }
            if (log.ExitCode is int exit) rows.Add(new("Installer exit code", ExitCodeText(exit), exit == 0 ? InfoTone.Normal : InfoTone.Critical));
            if (log.EnforcementErrorCode is long code and not 0)
            {
                var failure = new AppInstallFailure(unchecked((int)code), "", null);
                rows.Add(new("Error code", failure.Summary, InfoTone.Critical));
            }
            if (log.DetectedVersion.Length > 0) rows.Add(new("Version found", log.DetectedVersion));
            if (log.DownloadProblem.Length > 0) rows.Add(new("Download", log.DownloadProblem, InfoTone.Critical));
            foreach (var error in log.Errors.Where(error => error != log.DownloadProblem).Take(3)) rows.Add(new("Error in the log", error, InfoTone.Critical));
            return rows;
        }
    }

    /// <summary>The run's lines, for reading and copying: "10-16 06:12:03  message".</summary>
    public string LogExcerpt => Log is { Excerpt.Count: > 0 } log
        ? string.Join(Environment.NewLine, log.Excerpt.Select(line =>
            $"{line.At?.ToString("MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "",-14} {(line.Severity >= 3 ? "ERROR " : line.Severity == 2 ? "WARN  " : "")}{Flatten(line.Message)}"))
        : "";

    public bool HasLogExcerpt => LogExcerpt.Length > 0;

    public string LogExcerptHeader => $"Log lines ({Log?.Excerpt.Count ?? 0})";

    /// <summary>A Collect diagnostics zip downloaded from Intune, or log files copied from the device.</summary>
    public async Task ReadLogFilesAsync(IReadOnlyList<string> paths)
    {
        if (_logs is null || IsReadingLog || paths.Count == 0) return;
        IsReadingLog = true;
        LogMessage = $"Reading {string.Join(", ", paths.Select(Path.GetFileName))}…";
        try
        {
            var files = await _logs.FromFilesAsync(paths, _logCancellation.Token);
            Log = files.Analyze(App.AppId);
            LogMessage = null;
            if (_loaded) Rediagnose();
        }
        catch (DeviceLogException error)
        {
            LogMessage = error.Message;
        }
        catch (OperationCanceledException)
        {
            LogMessage = null;
        }
        finally
        {
            IsReadingLog = false;
        }
    }

    /// <summary>"1603: Windows Installer: fatal error during installation." for the installer's own code.</summary>
    private static string ExitCodeText(int exit)
    {
        if (exit == 0) return "0 (success)";
        return exit is > 0 and <= 0xFFFF && InstallErrorText.Meaning(unchecked((int)(0x80070000u | (uint)exit))) is { } meaning && !meaning.StartsWith("Windows error", StringComparison.Ordinal)
            ? $"{exit}: {meaning}"
            : exit.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Flatten(string message)
    {
        var flat = message.ReplaceLineEndings(" ");
        return flat.Length > 600 ? flat[..600] + "…" : flat;
    }

    // ---------- loading ----------

    [RelayCommand]
    public async Task LoadAsync()
    {
        _cancellation.Cancel();
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        Diagnosis = null;
        Details = SectionState<AppDetails>.Loading;
        Targeting = SectionState<AppTargeting>.Loading;
        Report = null;
        ReportNote = null;
        FilterNote = null;

        var details = LoadDetailsAsync(token);
        var report = LoadReportAsync(token);
        var groups = LoadGroupsAsync(token);
        var hardware = LoadHardwareAsync(token);
        await Task.WhenAll(details, report, groups, hardware);
        if (token.IsCancellationRequested) return;

        Facts = AppDeviceFacts.From(Device, await hardware);
        var filters = await LoadFiltersAsync(token);
        if (token.IsCancellationRequested) return;
        var (membership, groupsError) = await groups;
        Targeting = Details switch
        {
            { Kind: SectionKind.Loaded, Value: { } app } when membership is not null => SectionState<AppTargeting>.Loaded(AppTargeting.Resolve(app.Assignments, membership, filters)),
            { Kind: SectionKind.Loaded } => SectionState<AppTargeting>.Failed(groupsError ?? "FieldKit couldn't read the device's groups."),
            { Kind: SectionKind.Failed, Message: { } message } => SectionState<AppTargeting>.Failed(message),
            { Kind: SectionKind.NotFound } => SectionState<AppTargeting>.NotFound,
            _ => Targeting,
        };
        _loaded = true;
        Rediagnose();
    }

    /// <summary>Works out the diagnosis from everything read so far; again whenever the device's log arrives.</summary>
    private void Rediagnose() =>
        Diagnosis = AppInstallDecoder.Diagnose(new AppInstallEvidence
        {
            App = App,
            Device = Facts,
            Report = Report ?? App.Failure,
            Details = Details.Value,
            Targeting = Targeting.Value,
            Log = Log,
            DeviceApps = Subject.DeviceApps,
            Now = _time.GetUtcNow(),
        });

    public void Cancel()
    {
        _cancellation.Cancel();
        _logCancellation.Cancel();
    }

    private async Task LoadDetailsAsync(CancellationToken token)
    {
        try
        {
            var app = await _lookup.AppAsync(App.AppId, token);
            if (!token.IsCancellationRequested) Details = SectionState<AppDetails>.Loaded(app);
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            Details = SectionState<AppDetails>.NotFound;
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AppAssignments) is not { } message) return;
            Details = SectionState<AppDetails>.Failed(message);
        }
    }

    private async Task LoadReportAsync(CancellationToken token)
    {
        try
        {
            var report = await _lookup.ReportAsync(App.AppId, Device, token);
            if (!token.IsCancellationRequested) Report = report;
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AppInstallStatus) is not { } message) return;
            ReportNote = $"FieldKit couldn't read it: {message}";
        }
    }

    /// <summary>The device's groups and its primary user's, or why they couldn't be read.</summary>
    private async Task<(DeviceMembership? Membership, string? Error)> LoadGroupsAsync(CancellationToken token)
    {
        try
        {
            var upn = Device.UserPrincipalName;
            var deviceGroups = Subject.EntraObjectId is { } objectId ? _lookup.GroupsAsync($"devices/{objectId}", token) : Task.FromResult<IReadOnlyList<DirectoryGroup>>([]);
            var userGroups = upn.Length > 0 ? _lookup.GroupsAsync($"users/{upn}", token) : Task.FromResult<IReadOnlyList<DirectoryGroup>>([]);
            await Task.WhenAll(deviceGroups, userGroups);
            return (new DeviceMembership(Names(await deviceGroups), Names(await userGroups), upn.Length > 0), null);
        }
        catch (Exception error)
        {
            return (null, FeatureErrors.Message(error, _onUnauthorized, GraphFeature.Groups));
        }
    }

    /// <summary>Architecture and memory are optional: without them those requirements just aren't judged.</summary>
    private async Task<HardwareDetails?> LoadHardwareAsync(CancellationToken token)
    {
        try
        {
            return await _lookup.HardwareAsync(Device.Id, token);
        }
        catch (Exception error)
        {
            FeatureErrors.Message(error, _onUnauthorized);
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, AssignmentFilter>> LoadFiltersAsync(CancellationToken token)
    {
        var ids = Details.Value?.Assignments.Where(assignment => assignment.HasFilter).Select(assignment => assignment.FilterId!).Distinct().ToList() ?? [];
        var found = await Task.WhenAll(ids.Select(async id =>
        {
            try
            {
                return await _lookup.FilterAsync(id, token);
            }
            catch (Exception error)
            {
                if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AssignmentFilters) is { } message) FilterNote ??= $"FieldKit couldn't read an assignment filter: {message}";
                return null;
            }
        }));
        return found.OfType<AssignmentFilter>().ToDictionary(filter => filter.Id);
    }

    private static Dictionary<string, string> Names(IReadOnlyList<DirectoryGroup> groups) =>
        groups.GroupBy(group => group.Id).ToDictionary(group => group.Key, group => group.First().DisplayName);
}
