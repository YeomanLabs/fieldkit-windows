using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FieldKit.Core.Domain;

/// <summary>
/// Picks out what the Intune Management Extension did with one app from its log. A run goes from
/// "Processing subgraph with app ids: …" to "Done processing subgraph." on the same thread; the
/// latest run with results counts (the extension often just skips an app that isn't due to be
/// checked again). From it come the applicability and detection results, the installer's exit
/// code, download trouble and errors; the app's last reporting state gives the error code.
/// Message shapes come from real AppWorkload.log files (2026-10-16); anything that doesn't match
/// is left out rather than guessed at.
/// </summary>
public static partial class AppLogAnalyzer
{
    /// <summary>How many lines of the run to show.</summary>
    public const int ExcerptLines = 60;

    public static AppLogReport Analyze(IReadOnlyList<ImeLogLine> lines, string appId, string source)
    {
        var mentions = Enumerable.Range(0, lines.Count).Where(i => Mentions(lines[i], appId)).ToList();
        if (mentions.Count == 0) return new AppLogReport { Source = source };

        var runs = Runs(lines, appId, mentions);
        // The latest run that did something; later ones that only skipped the app are noted.
        RunResult? chosen = null;
        var index = runs.Count - 1;
        for (; index >= 0 && chosen is null; index--)
        {
            var result = Read(lines, runs[index], appId);
            if (result.HasResults) chosen = result;
        }
        chosen ??= Read(lines, runs[^1], appId);
        var skippedSince = runs.Skip(runs.IndexOf(chosen.Run) + 1)
            .Any(run => Enumerable.Range(run.Start, run.End - run.Start + 1).Any(i => lines[i].Message.Contains("Skipping processing", StringComparison.OrdinalIgnoreCase)));

        var state = LastReportingState(lines, appId);
        return new AppLogReport
        {
            Source = source,
            Found = true,
            LastSeen = lines[mentions[^1]].At,
            Applicability = chosen.Applicability,
            Detections = chosen.Detections,
            Execution = chosen.Execution,
            ExecutionResult = chosen.ExecutionResult,
            ExitCode = chosen.ExitCode,
            EnforcementErrorCode = state?.ErrorCode,
            DetectedVersion = state?.Version ?? "",
            DownloadProblem = chosen.Download,
            Errors = chosen.Errors.Take(5).ToList(),
            Excerpt = chosen.Excerpt.TakeLast(ExcerptLines).ToList(),
            RunAt = lines[chosen.Run.Start].At,
            SkippedSince = skippedSince,
        };
    }

    /// <summary>"Detected" → "detected", "NotDetected" → "not detected", "RequirementsNotMet" → "requirements not met".</summary>
    public static string Words(string state) =>
        state.Length == 0 ? "" : CamelHump().Replace(state, " $1").Trim().ToLowerInvariant();

    private sealed record Run(int Start, int End);

    private sealed record RunResult(Run Run)
    {
        public string Applicability { get; set; } = "";
        public List<string> Detections { get; } = [];
        public string Execution { get; set; } = "";
        public string ExecutionResult { get; set; } = "";
        public int? ExitCode { get; set; }
        public string Download { get; set; } = "";
        public List<string> Errors { get; } = [];
        public List<ImeLogLine> Excerpt { get; } = [];

        public bool HasResults => Applicability.Length > 0 || Detections.Count > 0 || Execution.Length > 0;
    }

    /// <summary>
    /// Every run that names the app, oldest first: from "Processing subgraph with app ids" to the
    /// "Done processing subgraph." on its thread. Without any, one run over the last stretch of
    /// lines that mention it.
    /// </summary>
    private static List<Run> Runs(IReadOnlyList<ImeLogLine> lines, string appId, List<int> mentions)
    {
        var runs = new List<Run>();
        foreach (var start in mentions.Where(i => lines[i].Message.Contains("Processing subgraph with app ids", StringComparison.OrdinalIgnoreCase)))
        {
            var end = lines.Count - 1;
            for (var i = start + 1; i < lines.Count; i++)
            {
                if (lines[i].Thread == lines[start].Thread && lines[i].Message.Contains("Done processing subgraph", StringComparison.OrdinalIgnoreCase))
                {
                    end = i;
                    break;
                }
            }
            runs.Add(new Run(start, end));
        }
        if (runs.Count == 0) runs.Add(new Run(mentions[Math.Max(0, mentions.Count - ExcerptLines)], mentions[^1]));
        return runs;
    }

    private static RunResult Read(IReadOnlyList<ImeLogLine> lines, Run run, string appId)
    {
        var result = new RunResult(run);
        var id = Regex.Escape(appId);
        var executionThread = -1;
        var executing = false;
        for (var i = run.Start; i <= run.End; i++)
        {
            var line = lines[i];
            var message = line.Message;
            if (Regex.Match(message, $@"Applicability check for policy with id: {id} resulted in action status: \w+ and applicability state: (\w+)", RegexOptions.IgnoreCase) is { Success: true } applies)
            {
                result.Applicability = applies.Groups[1].Value;
            }
            if (Regex.Match(message, $@"Detection for policy with id: {id} resulted in action status: \w+ and detection state: (\w+)", RegexOptions.IgnoreCase) is { Success: true } detected)
            {
                result.Detections.Add(detected.Groups[1].Value);
            }
            if (Regex.Match(message, $@"Handler invoked with execution type: (\w+) for policy with id: {id}", RegexOptions.IgnoreCase) is { Success: true } invoked)
            {
                result.Execution = invoked.Groups[1].Value;
                result.ExecutionResult = "";
                result.ExitCode = null;
                executionThread = line.Thread;
                executing = true;
            }
            else if (executing && line.Thread == executionThread && ExecutionCompleted().Match(message) is { Success: true } completed)
            {
                result.ExecutionResult = completed.Groups[1].Value;
                executing = false;
            }
            // Detection scripts report an lpExitCode too, so only the one while the installer runs counts.
            if (executing && ExitCode().Match(message) is { Success: true } exit
                && int.TryParse(exit.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var code))
            {
                result.ExitCode = code;
            }
            var otherApp = NamesAnotherApp(message, appId) && !Mentions(line, appId);
            if (!otherApp && message.Contains("download", StringComparison.OrdinalIgnoreCase) && DownloadTrouble().IsMatch(message))
            {
                result.Download = message;
            }
            if (!otherApp && line.Severity >= 3 && !result.Errors.Contains(message)) result.Errors.Add(message);
            // Other apps run in the same stretch; their lines name their own IDs.
            if (Mentions(line, appId) || (!otherApp && (line.IsProblem || KeyLine().IsMatch(message)))) result.Excerpt.Add(line);
        }
        return result;
    }

    private static bool Mentions(ImeLogLine line, string appId) => line.Message.Contains(appId, StringComparison.OrdinalIgnoreCase);

    private static bool NamesAnotherApp(string message, string appId) =>
        AnyId().Matches(message).Any(match => !match.Value.Equals(appId, StringComparison.OrdinalIgnoreCase));

    /// <summary>The app's last reporting state: <c>… ReportingState: {"ApplicationId":"…","EnforcementErrorCode":-2016345060,…}</c>.</summary>
    private static (long? ErrorCode, string Version)? LastReportingState(IReadOnlyList<ImeLogLine> lines, string appId)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var message = lines[i].Message;
            if (!Mentions(lines[i], appId)) continue;
            var json = message.IndexOf("ReportingState: {", StringComparison.Ordinal) is var at and >= 0 ? at + "ReportingState: ".Length
                : message.IndexOf("based on report: {", StringComparison.Ordinal) is var report and >= 0 ? report + "based on report: ".Length
                : -1;
            if (json < 0) continue;
            try
            {
                using var document = JsonDocument.Parse(message[json..]);
                var root = document.RootElement;
                if (!root.TryGetProperty("ApplicationId", out var app) || !string.Equals(app.GetString(), appId, StringComparison.OrdinalIgnoreCase)) continue;
                long? error = root.TryGetProperty("EnforcementErrorCode", out var code) && code.ValueKind == JsonValueKind.Number ? code.GetInt64() : null;
                var version = root.TryGetProperty("DetectedIdentityVersion", out var detected) && detected.ValueKind == JsonValueKind.String ? detected.GetString() ?? "" : "";
                return (error, version);
            }
            catch (JsonException)
            {
                // A line cut short or changed shape: try an earlier one.
            }
        }
        return null;
    }

    [GeneratedRegex(@"\[Win32App\]\[ExecutionActionHandler\] Handler completed with action status: (\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExecutionCompleted();

    [GeneratedRegex(@"lpExitCode:?\s*(-?\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExitCode();

    [GeneratedRegex(@"fail|error|timed out|timeout|hash mismatch|could not|cannot", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadTrouble();

    [GeneratedRegex(@"lpExitCode|exit code|download|detection|applicability|execution|enforcement", RegexOptions.IgnoreCase)]
    private static partial Regex KeyLine();

    [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
    private static partial Regex CamelHump();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex AnyId();
}
