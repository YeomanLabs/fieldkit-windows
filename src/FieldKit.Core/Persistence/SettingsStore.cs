using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Services;

namespace FieldKit.Core.Persistence;

/// <summary>Local, non-sensitive preferences, saved as they change.</summary>
public sealed partial class SettingsStore : ObservableObject
{
    public const int RecentGroupTagLimit = 8;

    private const string StaleThresholdDaysKey = "staleThresholdDays";
    private const string NamingConventionRegexKey = "namingConventionRegex";
    private const string LastSiteKey = "lastSite";
    private const string LastAreaKey = "lastArea";
    private const string RecentGroupTagsKey = "recentGroupTags";

    private readonly ILocalStore _store;

    public SettingsStore(ILocalStore store)
    {
        _store = store;
        var days = int.TryParse(store.GetSetting(StaleThresholdDaysKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var saved) ? saved : 0;
        StaleThresholdDays = days is >= AuditSettings.MinStaleThresholdDays and <= AuditSettings.MaxStaleThresholdDays
            ? days
            : AuditSettings.DefaultStaleThresholdDays;
        NamingConventionRegex = store.GetSetting(NamingConventionRegexKey) ?? "";
        LastSite = store.GetSetting(LastSiteKey) ?? "";
        LastArea = store.GetSetting(LastAreaKey) ?? "";
        // Tags can't contain control characters (GroupTag.Normalized), so a newline separates them safely.
        RecentGroupTags = (store.GetSetting(RecentGroupTagsKey) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuditSettings))]
    public partial int StaleThresholdDays { get; set; }

    /// <summary>Matched against the whole Intune device name. Saved as typed; an invalid one turns the check off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuditSettings))]
    [NotifyPropertyChangedFor(nameof(NamingConventionIsValid))]
    public partial string NamingConventionRegex { get; set; }

    /// <summary>Remembered for the next site walk.</summary>
    [ObservableProperty]
    public partial string LastSite { get; set; }

    [ObservableProperty]
    public partial string LastArea { get; set; }

    /// <summary>Autopilot group tags used recently, newest first, offered as suggestions.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> RecentGroupTags { get; set; }

    public bool NamingConventionIsValid => AuditSettings.IsValidNamingRegex(NamingConventionRegex);

    public AuditSettings AuditSettings => new()
    {
        StaleThresholdDays = StaleThresholdDays,
        NamingConventionRegex = NamingConventionIsValid ? NamingConventionRegex : "",
    };

    public void RememberGroupTag(string tag)
    {
        if (tag.Length == 0) return;
        RecentGroupTags = new[] { tag }.Concat(RecentGroupTags.Where(existing => existing != tag)).Take(RecentGroupTagLimit).ToList();
    }

    public void ResetAll()
    {
        StaleThresholdDays = AuditSettings.DefaultStaleThresholdDays;
        NamingConventionRegex = "";
        LastSite = "";
        LastArea = "";
        RecentGroupTags = [];
    }

    partial void OnStaleThresholdDaysChanged(int value) =>
        _store.SetSetting(StaleThresholdDaysKey, value.ToString(CultureInfo.InvariantCulture));

    partial void OnNamingConventionRegexChanged(string value) => _store.SetSetting(NamingConventionRegexKey, value);

    partial void OnLastSiteChanged(string value) => _store.SetSetting(LastSiteKey, value);

    partial void OnLastAreaChanged(string value) => _store.SetSetting(LastAreaKey, value);

    partial void OnRecentGroupTagsChanged(IReadOnlyList<string> value) => _store.SetSetting(RecentGroupTagsKey, string.Join('\n', value));
}
