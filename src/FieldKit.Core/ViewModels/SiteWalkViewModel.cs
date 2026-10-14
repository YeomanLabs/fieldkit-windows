using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// An active site walk (ports <c>SiteWalkModel</c>): each scanned serial is saved, then looked up
/// and snapshotted. Windows adds pasting a list, processed in order like scans.
/// </summary>
public sealed partial class SiteWalkViewModel : ObservableObject
{
    private readonly ILocalStore _store;
    private readonly DeviceLookup _lookup;
    private readonly SettingsStore _settings;
    private readonly Action _onUnauthorized;
    private readonly TimeProvider _time;
    private readonly HashSet<string> _inFlight = [];

    public SiteWalkViewModel(SiteWalk walk, ILocalStore store, DeviceLookup lookup, SettingsStore settings, Action onUnauthorized, TimeProvider? time = null)
    {
        Walk = walk;
        _store = store;
        _lookup = lookup;
        _settings = settings;
        _onUnauthorized = onUnauthorized;
        _time = time ?? TimeProvider.System;
        foreach (var scan in store.Scans(walk.Id).Reverse()) Scans.Add(new ScanItem(scan));
        Scans.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ScannedHeader));
            OnPropertyChanged(nameof(HasScans));
        };
    }

    public SiteWalk Walk { get; }

    /// <summary>"Contoso HQ · Floor 2".</summary>
    public string Heading => DeviceText.Join(" · ", Walk.Site, Walk.Area);

    /// <summary>Newest first.</summary>
    public ObservableCollection<ScanItem> Scans { get; } = [];

    public bool HasScans => Scans.Count > 0;

    public string ScannedHeader => $"Scanned ({Scans.Count})";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBanner), nameof(BannerText), nameof(BannerIsWarning))]
    public partial ScanBanner? Banner { get; private set; }

    public bool HasBanner => Banner is not null;

    public string BannerText => Banner?.Text ?? "";

    public bool BannerIsWarning => Banner?.IsWarning == true;

    /// <summary>Adds a scan unless the serial is already in this walk, then looks it up. The task completes with the lookup.</summary>
    public Task Add(string serial)
    {
        if (Scans.Any(scan => scan.Serial == serial))
        {
            Banner = new ScanBanner($"{serial} already scanned", IsWarning: true);
            return Task.CompletedTask;
        }
        var item = new ScanItem(_store.AddScan(Walk.Id, serial, _time.GetUtcNow()));
        Scans.Insert(0, item);
        Banner = new ScanBanner($"Added {serial}", IsWarning: false);
        return ResolveAsync(item);
    }

    /// <summary>Adds a pasted list in order, skipping entries that aren't serials and ones already scanned.</summary>
    public async Task AddListAsync(string text)
    {
        var list = SerialList.Parse(text);
        var fresh = list.Serials.Where(serial => Scans.All(scan => scan.Serial != serial)).ToList();
        foreach (var serial in fresh) await Add(serial);
        var parts = new List<string> { fresh.Count == 1 ? "Added 1 serial." : $"Added {fresh.Count} serials." };
        var duplicates = list.Serials.Count - fresh.Count;
        if (duplicates > 0) parts.Add(duplicates == 1 ? "1 was already scanned." : $"{duplicates} were already scanned.");
        if (list.Invalid.Count > 0)
        {
            var shown = string.Join(", ", list.Invalid.Take(3)) + (list.Invalid.Count > 3 ? ", …" : "");
            parts.Add($"Skipped {list.Invalid.Count} that {(list.Invalid.Count == 1 ? "doesn't" : "don't")} look like serials: {shown}");
        }
        Banner = new ScanBanner(string.Join(" ", parts), IsWarning: list.Invalid.Count > 0 || fresh.Count == 0);
    }

    /// <summary>Re-runs lookups left pending (e.g. FieldKit was closed mid-walk).</summary>
    public Task ResumePendingAsync() =>
        Task.WhenAll(Scans.Where(scan => scan.IsPending).ToList().Select(ResolveAsync));

    public Task Retry(ScanItem item)
    {
        item.Record = item.Record with { Status = ScanStatus.Pending };
        return ResolveAsync(item);
    }

    public void Delete(ScanItem item)
    {
        _store.DeleteScan(item.Record.Id);
        Scans.Remove(item);
    }

    public void End() => _store.EndSiteWalk(Walk.Id, _time.GetUtcNow());

    private async Task ResolveAsync(ScanItem item)
    {
        var serial = item.Serial;
        if (!_inFlight.Add(serial)) return;
        try
        {
            var snapshot = await _lookup.SnapshotAsync(serial, _settings.AuditSettings);
            item.Record = item.Record.Applying(snapshot);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.Unauthorized)
        {
            item.Record = item.Record.Failing(error.UserMessage);
            _onUnauthorized();
        }
        catch (GraphException error)
        {
            item.Record = item.Record.Failing(error.UserMessage);
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            item.Record = item.Record.Failing(GraphException.Network().UserMessage);
        }
        finally
        {
            _inFlight.Remove(serial);
        }
        if (Scans.Contains(item)) _store.UpdateScan(item.Record);
    }
}
