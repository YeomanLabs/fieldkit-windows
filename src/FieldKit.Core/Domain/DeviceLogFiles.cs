using System.Text.RegularExpressions;

namespace FieldKit.Core.Domain;

/// <summary>
/// A device's Intune Management Extension logs, from a diagnostics zip or log files the engineer
/// opened. Win32 and WinGet apps are logged in AppWorkload.log (rolled over to
/// AppWorkload-&lt;date&gt;.log); extensions older than 2023 logged them in IntuneManagementExtension.log.
/// </summary>
/// <param name="Source">The names of the files opened, e.g. "DiagLogs-CTS-LT-01234.zip".</param>
public sealed partial record DeviceLogFiles(string Source, IReadOnlyList<DeviceLogFile> Files)
{
    /// <summary>The files worth reading, out of a folder's names: the current app log and the one before it.</summary>
    public static IReadOnlyList<string> Pick(IEnumerable<string> names)
    {
        var list = names.ToList();
        var picked = new List<string>();
        foreach (var family in new[] { "AppWorkload", "IntuneManagementExtension" })
        {
            // Rolled-over files carry their date (AppWorkload-20260929-140606.log), so the name sorts by age.
            if (list.Where(name => Rolled().Match(name) is { Success: true } match && match.Groups[1].Value.Equals(family, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault() is { } previous)
            {
                picked.Add(previous);
            }
            if (list.FirstOrDefault(name => name.Equals($"{family}.log", StringComparison.OrdinalIgnoreCase)) is { } current) picked.Add(current);
        }
        return picked;
    }

    /// <summary>
    /// What the logs say about the app: AppWorkload first, then the older IntuneManagementExtension
    /// log, then any other log the engineer opened (a copy saved under another name).
    /// </summary>
    public AppLogReport Analyze(string appId)
    {
        AppLogReport? report = null;
        string[] families = ["AppWorkload", "IntuneManagementExtension"];
        foreach (var family in families.Append(""))
        {
            var group = family.Length > 0
                ? Files.Where(file => file.Name.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                : Files.Where(file => !families.Any(known => file.Name.StartsWith(known, StringComparison.OrdinalIgnoreCase)));
            var lines = group
                .OrderBy(file => file.Name.Equals($"{family}.log", StringComparison.OrdinalIgnoreCase)) // the rolled-over file is older
                .SelectMany(file => ImeLog.Parse(file.Text))
                .ToList();
            if (lines.Count == 0) continue;
            report = AppLogAnalyzer.Analyze(lines, appId, Source);
            if (report.Found) return report;
        }
        return report ?? new AppLogReport { Source = Source };
    }

    [GeneratedRegex(@"^(AppWorkload|IntuneManagementExtension)-\d{8}-\d{6}\.log$", RegexOptions.IgnoreCase)]
    private static partial Regex Rolled();
}
