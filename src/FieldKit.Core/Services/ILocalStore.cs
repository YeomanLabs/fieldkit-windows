using FieldKit.Core.Persistence;

namespace FieldKit.Core.Services;

/// <summary>
/// FieldKit's local data: settings, recent lookups and site walks. Only what iOS stores; never
/// secrets or tokens.
/// </summary>
public interface ILocalStore
{
    /// <summary>True when the database couldn't be opened and a temporary in-memory one stands in.</summary>
    bool IsTemporary { get; }

    string? GetSetting(string key);

    void SetSetting(string key, string value);

    void RemoveSetting(string key);

    /// <summary>The newest first, at most <see cref="SqliteLocalStore.RecentLookupLimit"/>.</summary>
    IReadOnlyList<RecentLookup> RecentLookups();

    /// <summary>
    /// Records a lookup, moving an existing serial to the top and keeping the newest ones. A
    /// null <paramref name="deviceName"/> keeps the name already recorded.
    /// </summary>
    void RecordLookup(string serial, string? deviceName, DateTimeOffset lookedUpAt);

    /// <summary>Deletes all local data except the settings named in <paramref name="keepSettings"/>.</summary>
    void ClearLocalData(IReadOnlyCollection<string> keepSettings);

    /// <summary>Every site walk, newest first.</summary>
    IReadOnlyList<SiteWalk> SiteWalks();

    SiteWalk? SiteWalk(string id);

    SiteWalk StartSiteWalk(string site, string area, DateTimeOffset startedAt);

    void EndSiteWalk(string id, DateTimeOffset endedAt);

    /// <summary>Deletes the walk and its scans.</summary>
    void DeleteSiteWalk(string id);

    /// <summary>A walk's scans, oldest first.</summary>
    IReadOnlyList<ScanRecord> Scans(string walkId);

    /// <summary>Adds a pending scan.</summary>
    ScanRecord AddScan(string walkId, string serial, DateTimeOffset scannedAt);

    /// <summary>Saves a scan's status and snapshot.</summary>
    void UpdateScan(ScanRecord scan);

    void DeleteScan(long id);
}
