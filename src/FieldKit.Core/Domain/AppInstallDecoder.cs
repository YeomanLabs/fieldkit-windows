namespace FieldKit.Core.Domain;

/// <summary>
/// "Why didn't it install?": turns what Intune reports about one app on one device into a verdict,
/// the likely causes (most likely first) and what to check. It reads the error code (Intune's
/// detection code and Windows Installer exit codes), the app's requirements, detection rules and
/// return codes, its assignments and filters, its dependencies, and when the device last checked
/// in. What FieldKit can't see (script rules, filter results, the device's disk) is described,
/// never judged.
/// </summary>
public static class AppInstallDecoder
{
    /// <summary>Intune's code for "the installer finished, but the detection rule didn't find the app".</summary>
    public const uint DetectionFailed = 0x87D1041C;

    /// <summary>A device silent for longer than this is a likely reason nothing has happened.</summary>
    public static readonly TimeSpan LongSilence = TimeSpan.FromDays(7);

    /// <summary>Silent for longer than this is worth mentioning.</summary>
    public static readonly TimeSpan ShortSilence = TimeSpan.FromHours(24);

    /// <summary>Offered until the device's log has been read (Win32 and WinGet apps, which the extension installs).</summary>
    public const string ReadLogCheck =
        "Run Collect diagnostics on the device in Intune, then open the zip under Device log, below, to see what the Intune Management Extension did with this app.";

    public const string SyncCheck = "Sync the device to make it check in now.";

    /// <summary>Replaces <see cref="SyncCheck"/> when the device has been quiet.</summary>
    public const string WakeAndSyncCheck = "Make sure the device is turned on and online, then Sync it.";

    public static AppInstallDiagnosis Diagnose(AppInstallEvidence evidence) =>
        evidence.App.Outcome switch
        {
            AppInstallOutcome.Failed => Failed(evidence),
            AppInstallOutcome.NotApplicable => NotApplicable(evidence),
            AppInstallOutcome.Pending => Waiting(evidence, pending: true),
            AppInstallOutcome.NoStatus => Waiting(evidence, pending: false),
            AppInstallOutcome.Missing when evidence.App.Intent == AppIntent.RequiredUninstall => StillInstalled(evidence),
            AppInstallOutcome.Missing => NotInstalled(evidence),
            AppInstallOutcome.Installed => new(AppInstallVerdict.Installed, "Installed",
                $"Intune reports {evidence.App.Name} as installed on this device.", [], []),
            AppInstallOutcome.Removed => new(AppInstallVerdict.Uninstalled, "Uninstalled",
                $"{evidence.App.Name} is set to be uninstalled, and the device reports it's gone.", [], []),
            _ => new(AppInstallVerdict.Available, "Available in Company Portal",
                "It's offered as available, so it installs only when someone chooses it in Company Portal on the device. Nothing is wrong.",
                [], ["To install it now, open Company Portal on the device and choose it."]),
        };

    private static AppInstallDiagnosis Failed(AppInstallEvidence evidence)
    {
        var findings = new Findings();
        var uninstall = evidence.App.State == AppInstallState.UninstallFailed;
        var verb = uninstall ? "uninstall" : "install";
        // A failed dependency, or a download that never finished, is the root cause, so it goes first.
        DependencyCauses(evidence, findings);
        DownloadCause(evidence, findings);

        var report = evidence.Report;
        var log = evidence.Log;
        // Intune's report first; the device's own log stands in when the report has no code.
        var code = report is { ErrorCode: not 0 } ? unchecked((uint)report.ErrorCode)
            : log?.EnforcementErrorCode is long logged and not 0 ? unchecked((uint)logged)
            : 0u;
        string title;
        string explanation;
        if (code == DetectionFailed && !uninstall)
        {
            (title, explanation) = NotDetected(evidence, findings);
        }
        else if ((code & 0xFFFF0000) == 0x80070000)
        {
            (title, explanation) = ExitCode((int)(code & 0xFFFF), evidence, findings, verb);
        }
        else if (code != 0)
        {
            var hex = InstallErrorText.Hex(unchecked((int)code));
            title = uninstall ? "Uninstall failed" : "Install failed";
            explanation = $"Intune reported error {hex} for the {verb}.";
            findings.Likely($"Intune error {hex}", report is { Detail.Length: > 0 } ? IntuneWords(report.Detail) : "FieldKit doesn't know this code, and Intune gave no description.");
            findings.Check($"Search Microsoft's Intune app troubleshooting documentation for {hex}.");
        }
        else if (log?.ExitCode is int exit and not 0)
        {
            (title, explanation) = ExitCode(exit, evidence, findings, verb);
        }
        else
        {
            title = uninstall ? "Uninstall failed" : "Install failed";
            explanation = report is null && log is null
                ? $"Intune reports that the {verb} failed, but FieldKit couldn't read the error code from Intune's install report."
                : $"Intune reports that the {verb} failed, without an error code.";
        }
        LogErrorCause(evidence, findings);

        if (!uninstall)
        {
            foreach (var replaced in Relationships(evidence).Where(relationship => relationship is { Kind: AppRelationshipKind.Supersedence, TargetIsChild: true, Replaces: true }))
            {
                findings.Possible($"It replaces {replaced.TargetName}",
                    $"Intune uninstalls {replaced.TargetName} before installing this app. If that uninstall fails, this install fails too.");
            }
        }
        LogCheck(evidence, findings);
        return new(AppInstallVerdict.Failed, title, explanation, findings.Causes, findings.Checks);
    }

    private static (string Title, string Explanation) NotDetected(AppInstallEvidence evidence, Findings findings)
    {
        var win32 = evidence.Details?.Win32;
        var rules = win32?.DetectionRules ?? [];
        if (rules.Count == 0)
        {
            findings.Likely("The detection rule doesn't match what was installed",
                win32 is null ? "FieldKit couldn't read the app's detection rule." : "The app has no detection rule FieldKit can read.");
        }
        else if (rules.All(rule => rule.Kind == AppRuleKind.Script))
        {
            findings.Likely("The detection script didn't report the app",
                $"Detection is a PowerShell script. Intune counts the app as installed only when the script exits with 0 and writes something to standard output. A script that writes nothing, or fails, means not installed.{LogAgrees(evidence)}");
            findings.Check("Run the detection script on the device as SYSTEM and check that it writes something and exits with 0.");
        }
        else
        {
            var looksFor = rules.Count == 1
                ? $"Intune looks for this: {rules[0].Description}."
                : $"Intune looks for all of these: {string.Join("; ", rules.Select(rule => rule.Description))}.";
            findings.Likely("The detection rule doesn't match what was installed",
                $"{looksFor} If the installer put the app somewhere else (Program Files (x86), a per-user folder) or installed a different version, the rule won't match.{LogAgrees(evidence)}");
            findings.Check("Install the app on a test PC, then compare where it actually went (folder, registry key, version) with the detection rule.");
        }
        if (win32 is { InstallsAsUser: true } && rules.Any(AppRuleText.LooksMachineWide))
        {
            findings.Possible("It installs for the user, but detection looks machine-wide",
                "The app installs in the signed-in user's context. Per-user installers usually go to the user's profile (AppData and HKEY_CURRENT_USER), where a rule that looks in Program Files or HKEY_LOCAL_MACHINE won't find them.");
        }
        return ("Installed, but not detected",
            "The installer finished without an error, but afterwards the app's detection rule didn't find the app, so Intune counts the install as failed.");
    }

    /// <summary>0x8007xxxx: the installer's exit code (or a Windows error) in its low 16 bits.</summary>
    private static (string Title, string Explanation) ExitCode(int exit, AppInstallEvidence evidence, Findings findings, string verb)
    {
        var win32 = evidence.Details?.Win32;
        var mapped = win32?.ReturnCodes.FirstOrDefault(code => code.Code == exit);
        var ended = $"The {verb} ended with exit code {exit}";
        switch (exit)
        {
            case 1618:
                var retries = mapped?.Type.Equals("retry", StringComparison.OrdinalIgnoreCase) == true;
                findings.Likely("Windows Installer was busy",
                    "Another installation (an update, or another app) was running at the same time, so this one couldn't start. "
                    + (retries
                        ? "The app's return codes tell Intune to retry 1618, so this usually clears on its own."
                        : "The app's return codes don't mark 1618 as Retry, so Intune counts it as a failure."));
                if (!retries) findings.Check("Add 1618 to the app's return codes as Retry.");
                return ("Another installation was running", $"{ended}: Windows Installer was busy with another installation.");
            case 1603:
                findings.Likely("The installer failed on the device",
                    "1603 is Windows Installer's general “fatal error during installation”. Common reasons: a prerequisite is missing, another version is installed in a way this one can't upgrade, a restart is pending, or the app or a file it replaces was in use.");
                findings.Check("Run the install command on a test PC as SYSTEM (for example with psexec -s), adding msiexec's /l*v option for a verbose log. The log names the step that failed.");
                return ("The installer hit a fatal error", $"{ended}, Windows Installer's general fatal error.");
            case 1602 or 1223:
                findings.Likely("The installer was waiting for someone",
                    $"Exit code {exit} means the installation was cancelled, which usually means the installer showed a prompt or window that nobody answered. Intune runs installs silently, with no one to click.");
                findings.Check("Check the install command includes the installer's silent switch (msiexec /qn, or the vendor's own, such as /S or /silent).");
                return ("The installation was cancelled", $"{ended}: the installation was cancelled.");
            case 1625:
                findings.Likely("Blocked by policy", "Windows Installer refused the installation because a system policy forbids it.");
                findings.Check("Check AppLocker, App Control for Business (WDAC) and Windows Installer policies on the device.");
                return ("Blocked by policy", $"{ended}: a system policy blocked Windows Installer.");
            case 1638:
                findings.Likely("Another version is already installed", "A different version of the product is on the device, and this installer can't upgrade it.");
                findings.Check("Remove the other version first, or make this app supersede the old one in Intune so it's replaced.");
                return ("Another version is already installed", $"{ended}: another version of the product is already installed.");
            case 1619:
                var setup = win32 is { SetupFile.Length: > 0 } ? $" ({win32.SetupFile})" : "";
                findings.Likely("The installer package couldn't be opened",
                    $"Windows Installer couldn't open the package. The file named in the install command may not match the setup file in the .intunewin{setup}, or the download was damaged.");
                findings.Check("Compare the file name in the install command with the setup file in the package.");
                return ("The installer package couldn't be opened", $"{ended}: the installer package couldn't be opened.");
            case 3010 or 1641 when mapped is null:
                findings.Likely("The installer asked for a restart",
                    $"Exit code {exit} means it worked but needs a restart. The app's return codes don't list {exit}, so Intune counts it as a failure.");
                findings.Check($"Add {exit} to the app's return codes as a {(exit == 3010 ? "soft" : "hard")} restart.");
                return ("The installer asked for a restart", $"{ended}, which asks for a restart.");
            default:
                var listed = mapped is not null ? $" The app's return codes map {exit} to “{mapped.Type}”."
                    : win32 is { ReturnCodes.Count: > 0 } ? " It isn't one of the app's return codes, so Intune counts it as a failure."
                    : "";
                findings.Likely($"The installer exited with {exit}",
                    $"Intune reported {InstallErrorText.Hex(unchecked((int)(0x80070000u | (uint)exit)))}: code {exit} from the installer, or a Windows error with that number.{listed}");
                findings.Check($"Look up exit code {exit} in the installer's documentation. If it means success for this installer, add it to the app's return codes.");
                return ($"The installer exited with {exit}", $"{ended}.");
        }
    }

    private static AppInstallDiagnosis NotApplicable(AppInstallEvidence evidence)
    {
        var findings = new Findings();
        var win32 = evidence.Details?.Win32;
        // The device's own verdict comes first when its log has one.
        if (evidence.Log is { Applicability.Length: > 0 } log && !log.Applicability.Equals("Applicable", StringComparison.OrdinalIgnoreCase))
        {
            findings.Likely("The device decided it doesn't apply",
                $"The device's log records the applicability check as “{AppLogAnalyzer.Words(log.Applicability)}”.");
        }
        if (win32 is not null)
        {
            foreach (var check in AppRequirements.Check(win32, evidence.Device).Where(check => check.Met == false))
            {
                findings.Likely($"Doesn't meet the {check.Label.ToLowerInvariant()} requirement", $"The app needs {check.Needs}; this device has {check.DeviceHas}.");
            }
        }
        FilterCauses(evidence, findings, canBeLikely: !findings.AnyLikely);
        if (win32 is { RequirementRules.Count: > 0 })
        {
            var rules = string.Join("; ", win32.RequirementRules.Select(rule => rule.Description));
            findings.Add(!findings.Any, "A requirement rule isn't met",
                $"The app also requires: {rules}. These run on the device, so FieldKit can't check them.");
        }
        if (!findings.Any)
        {
            if (evidence.Details is { Win32: null } details)
            {
                findings.Possible("Made for a different device",
                    $"It's a {details.TypeLabel}. Intune decides whether those apply (platform, Windows version or edition) without rules FieldKit can read.");
            }
            else if (win32 is not null)
            {
                findings.Possible("Nothing FieldKit can check failed",
                    "Every requirement FieldKit can check is met, so the reason is something only the device reports, such as its processor count or speed.");
            }
        }
        findings.Check("Compare the app's requirements in Intune (the app's Properties, Requirements) with this device.");
        LogCheck(evidence, findings);
        var explanation = "Intune checked the app's requirements and assignment filters against this device and decided it doesn't apply, so it won't install it.";
        return new(AppInstallVerdict.NotApplicable, "Doesn't apply to this device", WithIntuneWords(explanation, evidence), findings.Causes, findings.Checks);
    }

    private static AppInstallDiagnosis Waiting(AppInstallEvidence evidence, bool pending)
    {
        var findings = SilentCauses(evidence);
        findings.CheckSync();
        return pending
            ? new(AppInstallVerdict.Waiting, "Waiting to install", "Intune has sent the app to the device and is waiting for the install to finish.", findings.Causes, findings.Checks)
            : new(AppInstallVerdict.Waiting, "No status from the device yet", "The app is assigned here, but the device hasn't reported anything about it yet.", findings.Causes, findings.Checks);
    }

    private static AppInstallDiagnosis NotInstalled(AppInstallEvidence evidence)
    {
        var findings = SilentCauses(evidence);
        findings.CheckSync();
        return new(AppInstallVerdict.NotInstalled, "Not installed yet",
            WithIntuneWords("Intune reports the app isn't on the device, and there's no error. Usually the install just hasn't run yet.", evidence),
            findings.Causes, findings.Checks);
    }

    private static AppInstallDiagnosis StillInstalled(AppInstallEvidence evidence)
    {
        var findings = new Findings();
        var win32 = evidence.Details?.Win32;
        var command = win32 is { UninstallCommand.Length: > 0 } ? $" ({win32.UninstallCommand})" : "";
        var rules = win32?.DetectionRules ?? [];
        var detection = rules.Count > 0 ? $" The rule looks for: {string.Join("; ", rules.Select(rule => rule.Description))}." : "";
        findings.Likely("The uninstall hasn't run, or didn't remove it",
            $"Intune runs the uninstall command{command}, then checks the detection rule. While the rule still finds the app, it counts as installed.{detection}");
        DownloadCause(evidence, findings);
        LogErrorCause(evidence, findings);
        CheckInCauses(evidence, findings);
        ConflictCauses(evidence, findings);
        findings.CheckSync();
        LogCheck(evidence, findings);
        return new(AppInstallVerdict.NotInstalled, "Still installed",
            "The app is set to be uninstalled, but the device still reports it as installed.", findings.Causes, findings.Checks);
    }

    /// <summary>Why nothing has happened yet: the device, the dependencies, the requirements or the assignment.</summary>
    private static Findings SilentCauses(AppInstallEvidence evidence)
    {
        var findings = new Findings();
        CheckInCauses(evidence, findings);
        var log = evidence.Log;
        if (log is { Found: false })
        {
            findings.Likely("The device hasn't started on it",
                "The Intune Management Extension's log on the device doesn't mention this app at all, so the device hasn't picked it up yet.");
        }
        DownloadCause(evidence, findings);
        DependencyCauses(evidence, findings);
        if (evidence.Details?.Win32 is { } win32)
        {
            foreach (var check in AppRequirements.Check(win32, evidence.Device).Where(check => check.Met == false))
            {
                findings.Likely($"Doesn't meet the {check.Label.ToLowerInvariant()} requirement",
                    $"The app needs {check.Needs}; this device has {check.DeviceHas}. Intune will mark it not applicable rather than install it.");
            }
        }
        if (evidence.Targeting is { } targeting)
        {
            var installs = targeting.Includes.Where(match => !match.IsUninstall).ToList();
            if (installs.Count == 0 && evidence.App.Intent.IsRequired())
            {
                findings.Possible("FieldKit can't see which assignment reaches this device",
                    "None of the app's assignments match the device's groups or its primary user's. Intune may be targeting another user who signs in on this device.");
            }
            else if (installs.Count > 0 && installs.All(match => match.ThroughUser))
            {
                var user = evidence.Device.HasUser ? evidence.Device.UserName : "the user";
                findings.Possible("It's assigned to the user, not the device",
                    $"It reaches this device only through {user}'s groups, so Intune installs it once {user} signs in on this device.");
            }
            FilterCauses(evidence, findings, canBeLikely: false);
            foreach (var excluded in targeting.Excludes)
            {
                findings.Possible($"Excluded through {excluded.Via}",
                    "The device or its user is in a group this app's assignments exclude. Intune applies an exclusion over an inclusion of the same kind (user or device group).");
            }
        }
        ConflictCauses(evidence, findings);
        if (log is { Found: true })
        {
            if (log.LastDetection.Equals("Detected", StringComparison.OrdinalIgnoreCase))
            {
                findings.Possible("The device already finds it installed",
                    "The device's log says detection found the app, so Intune's status should catch up the next time the device reports in.");
            }
            else if (LastRun(log) is { Length: > 0 } lastRun)
            {
                findings.Possible("What the device did last", lastRun);
            }
            LogErrorCause(evidence, findings);
        }
        else if (log is null && HandledByExtension(evidence))
        {
            findings.Possible("The Intune Management Extension hasn't picked it up yet",
                "Win32 and WinGet apps are installed by the Intune Management Extension on the device, which checks for new required apps about once an hour, and after a restart.");
        }
        LogCheck(evidence, findings);
        return findings;
    }

    /// <summary>The extension installs Win32 and WinGet apps, so its log covers them; other types install through Windows' own MDM channel.</summary>
    private static bool HandledByExtension(AppInstallEvidence evidence) =>
        evidence.Details is not { } details || details.TypeName is AppTypeNames.Win32 or "winGetApp";

    /// <summary>Points at the device's log until it's been read.</summary>
    private static void LogCheck(AppInstallEvidence evidence, Findings findings)
    {
        if (evidence.Log is null && HandledByExtension(evidence)) findings.Check(ReadLogCheck);
    }

    private static void DownloadCause(AppInstallEvidence evidence, Findings findings)
    {
        if (evidence.Log is not { DownloadProblem.Length: > 0 } log) return;
        findings.Likely("The download didn't finish",
            $"The device's log reports trouble downloading the app: “{Clip(log.DownloadProblem)}”. Check the device's network, proxy and free disk space, and that Delivery Optimization isn't blocked.");
    }

    private static void LogErrorCause(AppInstallEvidence evidence, Findings findings)
    {
        if (evidence.Log is not { Errors.Count: > 0 } log) return;
        var first = log.Errors.FirstOrDefault(error => error != log.DownloadProblem);
        if (first is null) return;
        findings.Possible("The device's log shows errors", $"The first one: “{Clip(first)}”.{(log.Errors.Count > 1 ? $" {log.Errors.Count - 1} more under Device log." : "")}");
    }

    /// <summary>" The device's log agrees: the installer exited with 0, then detection found nothing." when it does.</summary>
    private static string LogAgrees(AppInstallEvidence evidence) =>
        evidence.Log is { Found: true } log && log.LastDetection.Equals("NotDetected", StringComparison.OrdinalIgnoreCase)
            ? log.ExitCode is int exit
                ? $" The device's log agrees: the installer exited with {exit}, then detection didn't find the app."
                : " The device's log agrees: detection didn't find the app."
            : "";

    /// <summary>"Last run: applicable; detection: not detected; install: failed, exit code 1603."</summary>
    private static string LastRun(AppLogReport log)
    {
        var parts = new List<string>();
        if (log.Applicability.Length > 0) parts.Add($"applicability {AppLogAnalyzer.Words(log.Applicability)}");
        if (log.Detections.Count > 0) parts.Add($"detection {string.Join(", then ", log.Detections.Select(AppLogAnalyzer.Words))}");
        if (log.Execution.Length > 0)
        {
            var result = log.ExecutionResult.Length > 0 ? AppLogAnalyzer.Words(log.ExecutionResult) : "no result logged";
            parts.Add($"{AppLogAnalyzer.Words(log.Execution)} {result}{(log.ExitCode is int exit ? $" (exit code {exit})" : "")}");
        }
        return parts.Count == 0 ? "" : $"The device's log, last run: {string.Join("; ", parts)}.";
    }

    private static string Clip(string text) => text.Length > 300 ? text[..300] + "…" : text;

    private static void CheckInCauses(AppInstallEvidence evidence, Findings findings)
    {
        var device = evidence.Device.Name.Length > 0 ? evidence.Device.Name : "the device";
        if (evidence.Device.LastSync is not { } synced)
        {
            findings.Likely("The device has never checked in", $"Intune has no check-in from {device}, so nothing can install or report.");
            return;
        }
        var silence = evidence.Now - synced;
        if (silence < ShortSilence) return;
        findings.Add(silence >= LongSilence, $"Hasn't checked in for {Span(silence)}",
            $"Intune last heard from {device} {Span(silence)} ago. Until it checks in again, nothing new installs or reports back.");
        findings.Check(WakeAndSyncCheck);
    }

    private static void DependencyCauses(AppInstallEvidence evidence, Findings findings)
    {
        foreach (var dependency in Relationships(evidence).Where(relationship => relationship.IsDependencyOfThisApp))
        {
            var name = dependency.TargetName.Length > 0 ? dependency.TargetName : "another app";
            var onDevice = evidence.DeviceApps.FirstOrDefault(app => app.AppId.Equals(dependency.TargetId, StringComparison.OrdinalIgnoreCase));
            if (onDevice is null)
            {
                findings.Possible($"Depends on {name}", dependency.AutoInstall
                    ? $"Intune installs {name} first. It isn't assigned to this device directly, so FieldKit can't see how that went; if it fails, this app fails too."
                    : $"{name} must already be installed: Intune only checks for it. It isn't assigned to this device, so if it isn't there, this app won't install.");
                continue;
            }
            switch (onDevice.Outcome)
            {
                case AppInstallOutcome.Failed:
                    findings.Likely($"Its dependency {name} failed", $"This app needs {name}, which failed on this device. Fix that first, and this app follows.");
                    break;
                case AppInstallOutcome.NotApplicable:
                    findings.Likely($"Its dependency {name} doesn't apply here", $"This app needs {name}, which Intune says doesn't apply to this device, so this app can't install either.");
                    break;
                case AppInstallOutcome.Pending or AppInstallOutcome.NoStatus or AppInstallOutcome.Missing:
                    findings.Possible($"Waiting for {name}", $"This app needs {name}, which isn't installed on this device yet.");
                    break;
            }
        }
    }

    private static void FilterCauses(AppInstallEvidence evidence, Findings findings, bool canBeLikely)
    {
        if (evidence.Targeting is not { } targeting) return;
        var filtered = targeting.Includes.Where(match => match.Assignment.HasFilter).ToList();
        if (filtered.Count == 0) return;
        // Likely only when every assignment that reaches the device goes through a filter.
        var likely = canBeLikely && filtered.Count == targeting.Includes.Count;
        foreach (var match in filtered)
        {
            var name = match.Filter is { } filter ? $"“{filter.Name}”" : "a filter FieldKit couldn't read";
            var rule = match.Filter is { Rule.Length: > 0 } withRule ? $" ({withRule.Rule})" : "";
            var which = match.Assignment.FilterMode == AssignmentFilterMode.Exclude ? "don't match" : "match";
            findings.Add(likely, match.Filter is not null ? $"Assignment filter {name}" : "An assignment filter",
                $"The {match.IntentTitle.ToLowerInvariant()} assignment through {match.Via} only reaches devices that {which} {name}{rule}. FieldKit can't evaluate filters.");
        }
        findings.Check("In Intune, open the device's Filter evaluation report to see whether each filter matched.");
    }

    private static void ConflictCauses(AppInstallEvidence evidence, Findings findings)
    {
        if (evidence.Targeting is not { } targeting) return;
        var install = targeting.Includes.FirstOrDefault(match => match.IsRequired);
        var uninstall = targeting.Includes.FirstOrDefault(match => match.IsUninstall);
        if (install is null || uninstall is null) return;
        findings.Possible("It's assigned to both install and uninstall",
            $"{install.Via} assigns it as Required and {uninstall.Via} as Uninstall. Intune settles the conflict by its own rules, which may not be what was meant.");
    }

    private static IEnumerable<AppRelationship> Relationships(AppInstallEvidence evidence) => evidence.Details?.Relationships ?? [];

    private static string WithIntuneWords(string explanation, AppInstallEvidence evidence) =>
        evidence.Report is { Detail.Length: > 0 } report ? $"{explanation} {IntuneWords(report.Detail)}" : explanation;

    /// <summary>"Intune's description: “The application was not detected”." (Intune's text sometimes ends in a full stop, sometimes not.)</summary>
    private static string IntuneWords(string detail) => $"Intune's description: “{detail.Trim().TrimEnd('.')}”.";

    /// <summary>"3 days", "2 weeks", "30 hours".</summary>
    private static string Span(TimeSpan span) =>
        span.TotalDays >= 14 ? $"{(int)(span.TotalDays / 7)} weeks"
        : span.TotalDays >= 2 ? $"{(int)span.TotalDays} days"
        : $"{(int)span.TotalHours} hours";

    /// <summary>Causes in the order found, likely ones first, and checks without repeats.</summary>
    private sealed class Findings
    {
        private readonly List<AppInstallCause> _likely = [];
        private readonly List<AppInstallCause> _possible = [];
        private readonly List<string> _checks = [];

        public bool Any => _likely.Count + _possible.Count > 0;

        public bool AnyLikely => _likely.Count > 0;

        public IReadOnlyList<AppInstallCause> Causes => [.. _likely, .. _possible];

        public IReadOnlyList<string> Checks => _checks;

        public void Likely(string title, string detail) => _likely.Add(new AppInstallCause(title, detail, true));

        public void Possible(string title, string detail) => _possible.Add(new AppInstallCause(title, detail, false));

        public void Add(bool likely, string title, string detail)
        {
            if (likely) Likely(title, detail);
            else Possible(title, detail);
        }

        public void Check(string text)
        {
            if (!_checks.Contains(text)) _checks.Add(text);
        }

        /// <summary>"Sync the device", unless the advice to wake it and sync is already there.</summary>
        public void CheckSync()
        {
            if (!_checks.Contains(WakeAndSyncCheck)) Check(SyncCheck);
        }
    }
}
