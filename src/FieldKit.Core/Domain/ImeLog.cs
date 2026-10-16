using System.Globalization;
using System.Text.RegularExpressions;

namespace FieldKit.Core.Domain;

/// <summary>
/// Reads the Intune Management Extension's logs (AppWorkload.log, IntuneManagementExtension.log),
/// which use CMTrace's format: <c>&lt;![LOG[message]LOG]!&gt;&lt;time="20:37:59.7485460" date="10-12-2026" component="AppWorkload" context="" type="1" thread="17" file=""&gt;</c>.
/// A message can span lines.
/// </summary>
public static partial class ImeLog
{
    public static IReadOnlyList<ImeLogLine> Parse(string text)
    {
        var lines = new List<ImeLogLine>();
        foreach (Match match in Entry().Matches(text))
        {
            lines.Add(new ImeLogLine(
                match.Groups["message"].Value.Trim(),
                Time(match.Groups["date"].Value, match.Groups["time"].Value),
                match.Groups["component"].Value,
                int.TryParse(match.Groups["type"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var type) ? type : 1,
                int.TryParse(match.Groups["thread"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var thread) ? thread : 0));
        }
        return lines;
    }

    /// <summary>"10-12-2026" and "20:37:59.7485460" (older logs add a bias, "+300"); null when unreadable.</summary>
    private static DateTime? Time(string date, string time)
    {
        var clock = time.Split('+', '-')[0];
        return DateTime.TryParseExact($"{date} {clock}", ["M-d-yyyy H:mm:ss.FFFFFFF", "M-d-yyyy H:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : null;
    }

    [GeneratedRegex("""<!\[LOG\[(?<message>.*?)\]LOG\]!><time="(?<time>[^"]*)"\s+date="(?<date>[^"]*)"\s+component="(?<component>[^"]*)"\s+context="[^"]*"\s+type="(?<type>\d*)"\s+thread="(?<thread>\d*)"[^>]*>""", RegexOptions.Singleline)]
    private static partial Regex Entry();
}
