using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Persistence;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Phase 2 batch pre-staging: set one group tag on every scanned serial's Autopilot record
/// (ports <c>PreStageModel</c>). Windows adds pasting a list of serials.
/// </summary>
public sealed partial class PreStageViewModel : ObservableObject
{
    private readonly DeviceLookup _lookup;
    private readonly AutopilotUpdates _updates;
    private readonly SettingsStore _settings;
    private readonly Action _onUnauthorized;

    public PreStageViewModel(DeviceLookup lookup, AutopilotUpdates updates, SettingsStore settings, Action onUnauthorized)
    {
        _lookup = lookup;
        _updates = updates;
        _settings = settings;
        _onUnauthorized = onUnauthorized;
        GroupTagText = settings.RecentGroupTags.FirstOrDefault() ?? "";
        Results.CollectionChanged += (_, _) => OnResultsChanged();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagToApply), nameof(HasTag), nameof(SuggestedTags), nameof(HasSuggestedTags))]
    public partial string GroupTagText { get; set; }

    /// <summary>Newest first. Each row updates itself as its serial is tagged.</summary>
    public ObservableCollection<PreStageResult> Results { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBanner), nameof(BannerText), nameof(BannerIsWarning))]
    public partial ScanBanner? Banner { get; private set; }

    public bool HasBanner => Banner is not null;

    public string BannerText => Banner?.Text ?? "";

    public bool BannerIsWarning => Banner?.IsWarning == true;

    /// <summary>The tag to apply, or null when it's empty or invalid (batch mode never clears tags).</summary>
    public string? TagToApply => GroupTag.Normalized(GroupTagText) is { Length: > 0 } tag ? tag : null;

    public bool HasTag => TagToApply is not null;

    public IReadOnlyList<string> SuggestedTags => _settings.RecentGroupTags.Where(tag => tag != GroupTagText).ToList();

    public bool HasSuggestedTags => SuggestedTags.Count > 0;

    public int TaggedCount => Results.Count(result => result.Status is PreStageStatus.Tagged);

    public bool HasResults => Results.Count > 0;

    public string ResultsHeader => $"Scanned ({Results.Count}, {TaggedCount} tagged)";

    /// <summary>Starts tagging one scanned or typed serial. The task completes when it's done.</summary>
    public Task Add(string serial)
    {
        if (TagToApply is not { } tag)
        {
            Banner = new ScanBanner("Enter a group tag first", IsWarning: true);
            return Task.CompletedTask;
        }
        if (Results.Any(result => result.Serial == serial))
        {
            Banner = new ScanBanner($"{serial} already scanned", IsWarning: true);
            return Task.CompletedTask;
        }
        var result = new PreStageResult(serial);
        Results.Insert(0, result);
        Banner = new ScanBanner($"Tagging {serial}…", IsWarning: false);
        _settings.RememberGroupTag(tag);
        return ApplyAsync(tag, result);
    }

    /// <summary>
    /// Tags a pasted list one serial at a time, in order, with the same per-serial results as
    /// scanning. Entries that aren't serials, and serials already in the list, are skipped.
    /// </summary>
    public async Task AddListAsync(string text)
    {
        if (TagToApply is null)
        {
            Banner = new ScanBanner("Enter a group tag first", IsWarning: true);
            return;
        }
        var list = SerialList.Parse(text);
        var fresh = list.Serials.Where(serial => Results.All(result => result.Serial != serial)).ToList();
        foreach (var serial in fresh)
        {
            await Add(serial);
        }
        Banner = ListSummary(fresh.Count, list.Serials.Count - fresh.Count, list.Invalid);
    }

    public Task Retry(PreStageResult result)
    {
        if (TagToApply is not { } tag) return Task.CompletedTask;
        result.Status = PreStageStatus.Working;
        OnResultsChanged();
        return ApplyAsync(tag, result);
    }

    private async Task ApplyAsync(string tag, PreStageResult result)
    {
        try
        {
            var records = await _lookup.AutopilotDevicesAsync(result.Serial);
            if (records.Count == 0)
            {
                result.Status = PreStageStatus.NotInAutopilot;
                return;
            }
            result.Model = records[0].Model;
            var changed = false;
            foreach (var record in records.Where(record => record.GroupTag != tag))
            {
                await _updates.SetGroupTagAsync(tag, record.Id);
                changed = true;
            }
            result.Status = changed ? new PreStageStatus.Tagged(tag) : new PreStageStatus.AlreadyTagged(tag);
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.Unauthorized)
        {
            result.Status = new PreStageStatus.Failed(error.UserMessage);
            _onUnauthorized();
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AutopilotEdit) is { } message)
            {
                result.Status = new PreStageStatus.Failed(message);
            }
        }
        finally
        {
            OnResultsChanged();
        }
    }

    private static ScanBanner ListSummary(int added, int duplicates, IReadOnlyList<string> invalid)
    {
        var parts = new List<string> { added == 1 ? "Added 1 serial." : $"Added {added} serials." };
        if (duplicates > 0) parts.Add(duplicates == 1 ? "1 was already in the list." : $"{duplicates} were already in the list.");
        if (invalid.Count > 0)
        {
            var shown = string.Join(", ", invalid.Take(3)) + (invalid.Count > 3 ? ", …" : "");
            parts.Add($"Skipped {invalid.Count} that {(invalid.Count == 1 ? "doesn't" : "don't")} look like serials: {shown}");
        }
        return new ScanBanner(string.Join(" ", parts), IsWarning: invalid.Count > 0 || added == 0);
    }

    private void OnResultsChanged()
    {
        OnPropertyChanged(nameof(TaggedCount));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultsHeader));
    }
}
