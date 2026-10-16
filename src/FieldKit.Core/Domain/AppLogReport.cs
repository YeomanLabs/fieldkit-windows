namespace FieldKit.Core.Domain;

/// <summary>What the Intune Management Extension's log on the device says about one app, from its last run.</summary>
public sealed record AppLogReport
{
    /// <summary>Where the log came from: the names of the files opened, e.g. "DiagLogs-CTS-LT-01234.zip".</summary>
    public string Source { get; init; } = "";

    /// <summary>The log mentions the app at all.</summary>
    public bool Found { get; init; }

    /// <summary>The last time the log mentions the app, in the device's local time.</summary>
    public DateTime? LastSeen { get; init; }

    /// <summary>"Applicable", or the device's word for why not; empty when the log doesn't say.</summary>
    public string Applicability { get; init; } = "";

    /// <summary>Detection results in the last run, in order: before the install, then after it ("Detected", "NotDetected").</summary>
    public IReadOnlyList<string> Detections { get; init; } = [];

    /// <summary>"Install" or "Uninstall" when the last run ran the installer.</summary>
    public string Execution { get; init; } = "";

    /// <summary>"Success" or "Failed".</summary>
    public string ExecutionResult { get; init; } = "";

    /// <summary>The installer's exit code (the process's lpExitCode) in the last run.</summary>
    public int? ExitCode { get; init; }

    /// <summary>The error Intune reports for the app (EnforcementErrorCode in the extension's reporting state).</summary>
    public long? EnforcementErrorCode { get; init; }

    /// <summary>The version detection found, when the extension recorded one.</summary>
    public string DetectedVersion { get; init; } = "";

    /// <summary>The last line about a failed or stalled download in the last run; empty when there's none.</summary>
    public string DownloadProblem { get; init; } = "";

    /// <summary>Error lines in the last run, without repeats.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>The lines worth reading from the run.</summary>
    public IReadOnlyList<ImeLogLine> Excerpt { get; init; } = [];

    /// <summary>When the run these results come from started, in the device's local time.</summary>
    public DateTime? RunAt { get; init; }

    /// <summary>The extension has passed over the app since that run, because it wasn't due to be checked again.</summary>
    public bool SkippedSince { get; init; }

    public string LastDetection => Detections.Count > 0 ? Detections[^1] : "";
}
