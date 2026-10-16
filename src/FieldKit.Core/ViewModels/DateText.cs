using System.Globalization;

namespace FieldKit.Core.ViewModels;

/// <summary>Dates as the pages show them (ports <c>DateText.swift</c>).</summary>
public static class DateText
{
    /// <summary>"3 days ago · Oct 7, 2026 at 9:12 AM", or "Never".</summary>
    public static string RelativeAndAbsolute(DateTimeOffset? date, DateTimeOffset? now = null, CultureInfo? culture = null, TimeZoneInfo? zone = null) =>
        date is { } value ? $"{Relative(value, now)} · {Absolute(value, culture, zone)}" : "Never";

    /// <summary>Date and time in the user's format with an abbreviated month, or "—".</summary>
    public static string Absolute(DateTimeOffset? date, CultureInfo? culture = null, TimeZoneInfo? zone = null)
    {
        if (date is not { } value) return "—";
        var format = culture ?? CultureInfo.CurrentCulture;
        var local = TimeZoneInfo.ConvertTime(value, zone ?? TimeZoneInfo.Local);
        return $"{local.ToString(DatePattern(format), format)} at {local.ToString(format.DateTimeFormat.ShortTimePattern, format)}";
    }

    /// <summary>"Oct 16, 2026 at 6:12 AM (the device's clock)": a time from a device's log, which has no time zone.</summary>
    public static string DeviceClock(DateTime at, CultureInfo? culture = null)
    {
        var format = culture ?? CultureInfo.CurrentCulture;
        return $"{at.ToString(DatePattern(format), format)} at {at.ToString(format.DateTimeFormat.ShortTimePattern, format)} (the device's clock)";
    }

    /// <summary>The date alone, e.g. for telling duplicate records apart.</summary>
    public static string ShortDate(DateTimeOffset date, CultureInfo? culture = null, TimeZoneInfo? zone = null)
    {
        var format = culture ?? CultureInfo.CurrentCulture;
        return TimeZoneInfo.ConvertTime(date, zone ?? TimeZoneInfo.Local).ToString(DatePattern(format), format);
    }

    /// <summary>"just now", "5 minutes ago", "yesterday", "3 weeks ago", "in 20 days".</summary>
    public static string Relative(DateTimeOffset date, DateTimeOffset? now = null)
    {
        var span = (now ?? DateTimeOffset.Now) - date;
        var future = span < TimeSpan.Zero;
        var length = span.Duration();
        if (length < TimeSpan.FromMinutes(1)) return "just now";
        if (length >= TimeSpan.FromDays(1) && length < TimeSpan.FromDays(2)) return future ? "tomorrow" : "yesterday";
        var amount =
            length < TimeSpan.FromHours(1) ? Count((int)length.TotalMinutes, "minute")
            : length < TimeSpan.FromDays(1) ? Count((int)length.TotalHours, "hour")
            : length < TimeSpan.FromDays(7) ? Count((int)length.TotalDays, "day")
            : length < TimeSpan.FromDays(31) ? Count((int)(length.TotalDays / 7), "week")
            : length < TimeSpan.FromDays(365) ? Count(Math.Max(1, (int)(length.TotalDays / 30.44)), "month")
            : Count((int)(length.TotalDays / 365.25), "year");
        return future ? $"in {amount}" : $"{amount} ago";
    }

    private static string Count(int value, string unit) => value == 1 ? $"1 {unit}" : $"{value} {unit}s";

    /// <summary>Month-first cultures get "Oct 7, 2026"; everyone else "7 Oct 2026".</summary>
    private static string DatePattern(CultureInfo culture) =>
        culture.DateTimeFormat.ShortDatePattern.TrimStart().StartsWith('M') ? "MMM d, yyyy" : "d MMM yyyy";
}
