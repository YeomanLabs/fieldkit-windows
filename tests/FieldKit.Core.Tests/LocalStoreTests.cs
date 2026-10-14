using FieldKit.Core.Persistence;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows: the SQLite store that replaces SwiftData and UserDefaults.</summary>
public sealed class LocalStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_791_000_000);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FieldKitTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        using var store = SqliteLocalStore.OpenInMemory();
        Assert.Null(store.GetSetting("k"));
        store.SetSetting("k", "one");
        store.SetSetting("k", "two");
        Assert.Equal("two", store.GetSetting("k"));
        store.RemoveSetting("k");
        Assert.Null(store.GetSetting("k"));
    }

    [Fact]
    public void RecentLookupsKeepTheNewestTwentyAndMoveRepeatsToTheTop()
    {
        using var store = SqliteLocalStore.OpenInMemory();
        for (var index = 0; index < 25; index++)
        {
            store.RecordLookup($"SERIAL{index:00}", $"PC-{index}", Start.AddMinutes(index));
        }
        var recents = store.RecentLookups();
        Assert.Equal(SqliteLocalStore.RecentLookupLimit, recents.Count);
        Assert.Equal("SERIAL24", recents[0].Serial);
        Assert.Equal("SERIAL05", recents[^1].Serial);

        // Looking a serial up again moves it to the top and keeps its name when the new lookup found none.
        store.RecordLookup("SERIAL10", null, Start.AddHours(1));
        recents = store.RecentLookups();
        Assert.Equal("SERIAL10", recents[0].Serial);
        Assert.Equal("PC-10", recents[0].DeviceName);
        Assert.Equal(Start.AddHours(1), recents[0].LookedUpAt);
        Assert.Equal(SqliteLocalStore.RecentLookupLimit, recents.Count);
    }

    [Fact]
    public void ClearingKeepsOnlyTheNamedSettings()
    {
        using var store = SqliteLocalStore.OpenInMemory();
        store.SetSetting(AuthConfigStore.TenantIdKey, "contoso.onmicrosoft.com");
        store.SetSetting("staleThresholdDays", "7");
        store.RecordLookup("5CG1234XYZ", "PC1", Start);

        store.ClearLocalData(AuthConfigStore.Keys);
        Assert.Equal("contoso.onmicrosoft.com", store.GetSetting(AuthConfigStore.TenantIdKey));
        Assert.Null(store.GetSetting("staleThresholdDays"));
        Assert.Empty(store.RecentLookups());
    }

    [Fact]
    public void DataSurvivesReopeningTheFile()
    {
        var path = Path.Combine(_folder, "fieldkit.db");
        using (var store = SqliteLocalStore.Open(path))
        {
            Assert.False(store.IsTemporary);
            store.SetSetting("k", "v");
            store.RecordLookup("5CG1234XYZ", "PC1", Start);
        }
        using (var reopened = SqliteLocalStore.Open(path))
        {
            Assert.Equal("v", reopened.GetSetting("k"));
            Assert.Single(reopened.RecentLookups());
        }
    }

    [Fact]
    public void AnUnopenableFileFallsBackToATemporaryStore()
    {
        Directory.CreateDirectory(_folder);
        // A directory where the database file should be can't be opened as one.
        var path = Path.Combine(_folder, "taken");
        Directory.CreateDirectory(path);
        using var store = SqliteLocalStore.Open(path);
        Assert.True(store.IsTemporary);
        store.SetSetting("k", "v");
        Assert.Equal("v", store.GetSetting("k"));
    }

    [Fact]
    public void SettingsStoreLoadsSavesAndResets()
    {
        using var store = SqliteLocalStore.OpenInMemory();
        var settings = new SettingsStore(store);
        Assert.Equal(AuditSettings.DefaultStaleThresholdDays, settings.StaleThresholdDays);

        settings.StaleThresholdDays = 7;
        settings.NamingConventionRegex = "CTS-.*";
        settings.RememberGroupTag("CTS-Kiosk");
        settings.RememberGroupTag("CTS-Standard");
        settings.RememberGroupTag("CTS-Kiosk");

        var reloaded = new SettingsStore(store);
        Assert.Equal(7, reloaded.StaleThresholdDays);
        Assert.Equal("CTS-.*", reloaded.AuditSettings.NamingConventionRegex);
        Assert.Equal(["CTS-Kiosk", "CTS-Standard"], reloaded.RecentGroupTags);

        reloaded.ResetAll();
        var reset = new SettingsStore(store);
        Assert.Equal(AuditSettings.DefaultStaleThresholdDays, reset.StaleThresholdDays);
        Assert.Empty(reset.RecentGroupTags);
    }

    [Fact]
    public void SettingsStoreIgnoresBadSavedValues()
    {
        using var store = SqliteLocalStore.OpenInMemory();
        store.SetSetting("staleThresholdDays", "9000");
        store.SetSetting("namingConventionRegex", "([unclosed");
        var settings = new SettingsStore(store);
        Assert.Equal(AuditSettings.DefaultStaleThresholdDays, settings.StaleThresholdDays);
        // An invalid pattern is kept for editing but turns the check off.
        Assert.Equal("([unclosed", settings.NamingConventionRegex);
        Assert.False(settings.NamingConventionIsValid);
        Assert.Equal("", settings.AuditSettings.NamingConventionRegex);
    }

    [Fact]
    public void ClearAllResetsSettingsAndKeepsTenantSetup()
    {
        using var store = SqliteLocalStore.OpenInMemory();
        var settings = new SettingsStore(store) { StaleThresholdDays = 9 };
        AuthConfigStore.Save(new AuthConfig("1a2b3c4d-1111-2222-3333-444455556666", "contoso.onmicrosoft.com"), store);
        store.RecordLookup("5CG1234XYZ", null, Start);

        LocalDataCleaner.ClearAll(store, settings);
        Assert.Equal(AuditSettings.DefaultStaleThresholdDays, settings.StaleThresholdDays);
        Assert.Empty(store.RecentLookups());
        Assert.NotNull(AuthConfigStore.Saved(store));
    }
}
