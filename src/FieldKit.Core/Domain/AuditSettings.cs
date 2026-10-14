using System.Text.RegularExpressions;

namespace FieldKit.Core.Domain;

public sealed record AuditSettings
{
    public const int DefaultStaleThresholdDays = 30;
    public const int MinStaleThresholdDays = 1;
    public const int MaxStaleThresholdDays = 365;

    /// <summary>A naming pattern that takes longer than this on one name counts as not matching.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public int StaleThresholdDays { get; init; } = DefaultStaleThresholdDays;

    /// <summary>Matched against the whole Intune device name, ignoring case. Empty disables the check.</summary>
    public string NamingConventionRegex { get; init; } = "";

    public static bool IsValidNamingRegex(string pattern) =>
        pattern.Length == 0 || CompileNamingRegex(pattern) is not null;

    /// <summary>A whole-name, case-insensitive matcher for the pattern; null if it isn't a valid regex.</summary>
    public static Regex? CompileNamingRegex(string pattern)
    {
        try
        {
            // Validate the pattern as typed first: wrapping it could make an invalid one parse.
            _ = new Regex(pattern, RegexOptions.None, RegexTimeout);
            return new Regex($@"\A(?:{pattern})\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
