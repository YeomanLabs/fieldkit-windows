using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// The Site walks page: resume the walk in progress, start one (ports <c>StartSiteWalkSheet</c>),
/// and past walks (ports <c>SiteWalkHistoryView</c>).
/// </summary>
public sealed partial class SiteWalksViewModel : ObservableObject
{
    private readonly ILocalStore _store;
    private readonly SettingsStore _settings;
    private readonly TimeProvider _time;

    public SiteWalksViewModel(ILocalStore store, SettingsStore settings, TimeProvider? time = null)
    {
        _store = store;
        _settings = settings;
        _time = time ?? TimeProvider.System;
        SiteText = settings.LastSite;
        AreaText = settings.LastArea;
        Walks = [];
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    public partial string SiteText { get; set; }

    [ObservableProperty]
    public partial string AreaText { get; set; }

    public bool CanStart => SiteText.Trim().Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWalks), nameof(NoWalks), nameof(ActiveWalk), nameof(HasActiveWalk), nameof(ResumeText))]
    public partial IReadOnlyList<SiteWalkItem> Walks { get; private set; }

    public bool HasWalks => Walks.Count > 0;

    public bool NoWalks => Walks.Count == 0;

    /// <summary>The newest walk still in progress.</summary>
    public SiteWalk? ActiveWalk => Walks.FirstOrDefault(item => item.IsActive)?.Walk;

    public bool HasActiveWalk => ActiveWalk is not null;

    public string ResumeText => ActiveWalk is { } walk ? $"Resume {walk.Site}" : "";

    public bool StorageIsTemporary => _store.IsTemporary;

    public void Refresh() =>
        Walks = _store.SiteWalks().Select(walk => new SiteWalkItem(walk, _store.Scans(walk.Id).Count)).ToList();

    /// <summary>Starts a walk and remembers the site and area for next time. Null without a site.</summary>
    public SiteWalk? Start()
    {
        var site = SiteText.Trim();
        if (site.Length == 0) return null;
        var area = AreaText.Trim();
        _settings.LastSite = site;
        _settings.LastArea = area;
        var walk = _store.StartSiteWalk(site, area, _time.GetUtcNow());
        Refresh();
        return walk;
    }

    public void Delete(SiteWalk walk)
    {
        _store.DeleteSiteWalk(walk.Id);
        Refresh();
    }
}
