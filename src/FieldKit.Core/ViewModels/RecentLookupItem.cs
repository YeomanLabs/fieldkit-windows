using FieldKit.Core.Persistence;

namespace FieldKit.Core.ViewModels;

/// <summary>A recent lookup as the Lookup page lists it: the name (or serial), then serial and when.</summary>
public sealed record RecentLookupItem(string Serial, string Title, string Subtitle, string AutomationName)
{
    public static RecentLookupItem From(RecentLookup recent, DateTimeOffset? now = null)
    {
        var when = DateText.Relative(recent.LookedUpAt, now);
        return new RecentLookupItem(
            recent.Serial,
            recent.DeviceName ?? recent.Serial,
            recent.DeviceName is null ? when : $"{recent.Serial} · {when}",
            $"{recent.DeviceName ?? "Unnamed device"}, serial {recent.Serial}, looked up {when}");
    }
}
