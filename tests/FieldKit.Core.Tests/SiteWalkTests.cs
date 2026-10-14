using FieldKit.Core.Demo;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Persistence;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;
using Microsoft.Data.Sqlite;

namespace FieldKit.Core.Tests;

/// <summary>Site walks (M7): storage, scanning, pasted lists, the summary and the CSV.</summary>
public sealed class SiteWalkTests : IDisposable
{
    private readonly SqliteLocalStore _store = SqliteLocalStore.OpenInMemory();
    private readonly SettingsStore _settings;
    private readonly DeviceLookup _demo = new(new DemoGraphClient(TimeSpan.Zero));

    public SiteWalkTests()
    {
        _settings = new SettingsStore(_store);
    }

    public void Dispose() => _store.Dispose();

    private SiteWalkViewModel Walk(SiteWalk walk, DeviceLookup? lookup = null) => new(walk, _store, lookup ?? _demo, _settings, () => { });

    [Fact]
    public void StoreKeepsWalksAndScansAndDeletesThemTogether()
    {
        var first = _store.StartSiteWalk("Contoso HQ", "Floor 2", DateTimeOffset.UtcNow.AddHours(-2));
        var second = _store.StartSiteWalk("Depot", "", DateTimeOffset.UtcNow);
        var scan = _store.AddScan(first.Id, "5CG1234XYZ", DateTimeOffset.UtcNow);
        _store.UpdateScan(scan with { Status = ScanStatus.Done, DeviceName = "CTS-LT-01234", Flags = ["stale", "noncompliant"], WorstSeverity = "warning" });
        _store.AddScan(first.Id, "PF2ABCDE", DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(["Depot", "Contoso HQ"], _store.SiteWalks().Select(walk => walk.Site));
        var scans = _store.Scans(first.Id);
        Assert.Equal(["5CG1234XYZ", "PF2ABCDE"], scans.Select(found => found.Serial));
        Assert.Equal(ScanStatus.Done, scans[0].Status);
        Assert.Equal(["stale", "noncompliant"], scans[0].FlagValues);
        Assert.Equal(FlagSeverity.Warning, scans[0].Severity);
        Assert.Equal(ScanStatus.Pending, scans[1].Status);

        _store.EndSiteWalk(first.Id, DateTimeOffset.UtcNow);
        Assert.False(_store.SiteWalk(first.Id)!.IsActive);

        _store.DeleteSiteWalk(first.Id);
        Assert.Empty(_store.Scans(first.Id));
        Assert.Equal([second.Id], _store.SiteWalks().Select(walk => walk.Id));
    }

    [Fact]
    public void ClearingLocalDataRemovesSiteWalks()
    {
        var walk = _store.StartSiteWalk("Depot", "", DateTimeOffset.UtcNow);
        _store.AddScan(walk.Id, "5CG1234XYZ", DateTimeOffset.UtcNow);
        LocalDataCleaner.ClearAll(_store, _settings);
        Assert.Empty(_store.SiteWalks());
        Assert.Empty(_store.Scans(walk.Id));
    }

    [Fact]
    public void AnM1DatabaseUpgradesAndKeepsItsData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fieldkit-upgrade-{Guid.NewGuid():N}.db");
        try
        {
            using (var old = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                old.Open();
                using var create = old.CreateCommand();
                create.CommandText =
                    """
                    CREATE TABLE settings (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL);
                    CREATE TABLE recent_lookups (serial TEXT PRIMARY KEY NOT NULL, device_name TEXT, looked_up_at INTEGER NOT NULL);
                    INSERT INTO settings VALUES ('auth.tenantId', 'contoso.onmicrosoft.com');
                    INSERT INTO recent_lookups VALUES ('5CG1234XYZ', 'CTS-LT-01234', 1760000000000);
                    PRAGMA user_version = 1;
                    """;
                create.ExecuteNonQuery();
            }
            using var store = SqliteLocalStore.Open(path);
            Assert.False(store.IsTemporary);
            Assert.Equal("contoso.onmicrosoft.com", store.GetSetting("auth.tenantId"));
            Assert.Single(store.RecentLookups());
            var walk = store.StartSiteWalk("Depot", "", DateTimeOffset.UtcNow);
            Assert.Equal(walk.Id, Assert.Single(store.SiteWalks()).Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ScansAreLookedUpSnapshottedAndSaved()
    {
        var walk = _store.StartSiteWalk("Contoso HQ", "", DateTimeOffset.UtcNow);
        var model = Walk(walk);
        await model.Add("5CG1234XYZ");
        await model.Add("PF2ABCDE");
        await model.Add("5CG1234XYZ");
        Assert.Equal("5CG1234XYZ already scanned", model.BannerText);
        Assert.True(model.BannerIsWarning);
        Assert.Equal("Scanned (2)", model.ScannedHeader);

        // Newest first.
        Assert.Equal(["PF2ABCDE", "5CG1234XYZ"], model.Scans.Select(scan => scan.Serial));
        var healthy = model.Scans[1];
        Assert.True(healthy.IsDone);
        Assert.True(healthy.NoFlags);
        Assert.Equal("CTS-LT-01234", healthy.Title);
        Assert.True(model.Scans[0].HasFlags);

        // Saved: a new view model (e.g. after a restart) sees the same snapshots.
        var reopened = Walk(walk);
        Assert.Equal(ScanStatus.Done, reopened.Scans[1].Record.Status);
        Assert.Equal("CTS-LT-01234", reopened.Scans[1].Record.DeviceName);
    }

    [Fact]
    public async Task FailedLookupsCanBeRetriedAndPendingOnesResume()
    {
        var walk = _store.StartSiteWalk("Depot", "", DateTimeOffset.UtcNow);
        var failing = new DeviceLookup(new StubGraphClient([], _ => GraphException.Server(503, null)));
        var model = Walk(walk, failing);
        await model.Add("5CG1234XYZ");
        var scan = Assert.Single(model.Scans);
        Assert.True(scan.IsFailed);
        Assert.NotEmpty(scan.FailureText);

        var recovered = Walk(walk);
        await recovered.Retry(recovered.Scans[0]);
        Assert.True(recovered.Scans[0].IsDone);

        // A scan left pending (FieldKit closed mid-lookup) resumes.
        _store.AddScan(walk.Id, "PF9NEW01", DateTimeOffset.UtcNow);
        var resumed = Walk(walk);
        Assert.True(resumed.Scans[0].IsPending);
        await resumed.ResumePendingAsync();
        Assert.True(resumed.Scans[0].IsDone);
    }

    [Fact]
    public async Task APastedListIsScannedInOrder()
    {
        var walk = _store.StartSiteWalk("Depot", "", DateTimeOffset.UtcNow);
        var model = Walk(walk);
        await model.Add("PF9NEW01");
        await model.AddListAsync("5cg1234xyz\tPF2ABCDE\npf9new01\n???");
        Assert.Equal(["PF2ABCDE", "5CG1234XYZ", "PF9NEW01"], model.Scans.Select(scan => scan.Serial));
        Assert.Equal("Added 2 serials. 1 was already scanned. Skipped 1 that doesn't look like serials: ???", model.BannerText);
    }

    [Fact]
    public async Task DeleteAndEnd()
    {
        var walk = _store.StartSiteWalk("Depot", "", DateTimeOffset.UtcNow);
        var model = Walk(walk);
        await model.Add("5CG1234XYZ");
        model.Delete(model.Scans[0]);
        Assert.Empty(_store.Scans(walk.Id));
        model.End();
        Assert.False(_store.SiteWalk(walk.Id)!.IsActive);
    }

    [Fact]
    public async Task SummaryCountsFlagsAndExportsTheCsv()
    {
        var walk = _store.StartSiteWalk("Contoso HQ / East", "Floor 2", new DateTimeOffset(2026, 10, 12, 14, 0, 0, TimeSpan.Zero));
        var model = Walk(walk);
        await model.Add("5CG1234XYZ");
        await model.Add("PF2ABCDE");
        await model.Add("NOTREAL99");
        var failing = Walk(walk, new DeviceLookup(new StubGraphClient([], _ => GraphException.Forbidden())));
        await failing.Add("DENIED02");
        model.End();

        var summary = new SiteWalkSummaryViewModel(walk, _store);
        var details = summary.Details.ToDictionary(row => row.Label);
        Assert.Equal("4", details["Devices scanned"].Value);
        Assert.Equal(InfoTone.Caution, details["Lookups failed"].Tone);
        Assert.NotEqual("In progress", details["Ended"].Value);
        Assert.True(summary.HasFlags);
        Assert.Contains(summary.FlagCounts, row => row.Label == FlagKind.UnknownDevice.Title() && row.Value == "1");

        Assert.Equal("FieldKit-Contoso-HQ-East-2026-10-12.csv", SiteWalkExporter.FileName(walk, TimeZoneInfo.Utc));
        var rows = SiteWalkExporter.Rows(summary.Walk, summary.Scans);
        Assert.Equal(["5CG1234XYZ", "PF2ABCDE", "NOTREAL99", "DENIED02"], rows.Select(row => row.Serial));
        Assert.Equal(["lookupFailed"], rows[3].Flags);
        Assert.Empty(rows[0].Flags);

        var csv = summary.Csv();
        Assert.StartsWith("﻿site,area,scannedAt,serial", csv, StringComparison.Ordinal);
        Assert.Equal(5, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        var bytes = SiteWalkExporter.FileEncoding.GetBytes(csv);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'s' }, bytes[..4]);
    }

    [Fact]
    public void SiteWalksPageRemembersTheSiteAndFindsTheActiveWalk()
    {
        var page = new SiteWalksViewModel(_store, _settings);
        Assert.False(page.CanStart);
        Assert.True(page.NoWalks);
        page.SiteText = "  Contoso HQ ";
        page.AreaText = "Floor 2";
        var walk = page.Start()!;
        Assert.Equal("Contoso HQ", walk.Site);
        Assert.Equal("Contoso HQ", _settings.LastSite);
        Assert.Equal(walk.Id, page.ActiveWalk?.Id);
        Assert.Equal("Resume Contoso HQ", page.ResumeText);

        var next = new SiteWalksViewModel(_store, _settings);
        Assert.Equal("Contoso HQ", next.SiteText);
        Assert.Equal("Floor 2", next.AreaText);
        Assert.Contains("0 devices", next.Walks[0].Subtitle, StringComparison.Ordinal);

        next.Delete(walk);
        Assert.False(next.HasActiveWalk);
    }
}
