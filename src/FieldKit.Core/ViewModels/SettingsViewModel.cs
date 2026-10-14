using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// The Settings page (ports <c>SettingsView</c>): account, tenant setup, permissions, audit
/// rules, clearing local data and the version. No unlock section: the Windows app is free.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalStore _store;

    public SettingsViewModel(AppSession session, SettingsStore settings, ILocalStore store, string version)
    {
        Session = session;
        Settings = settings;
        _store = store;
        Version = version;
        Permissions = session.IsSignedIn ? new PermissionsViewModel(session.Auth) : null;
        settings.PropertyChanged += SettingsChanged;
    }

    public AppSession Session { get; }

    public SettingsStore Settings { get; }

    /// <summary>Null when signed out (there's no token to inspect).</summary>
    public PermissionsViewModel? Permissions { get; }

    public string Version { get; }

    [ObservableProperty]
    public partial string? ClearMessage { get; private set; }

    /// <summary>For a number box, which works in doubles; out-of-range and empty input are clamped.</summary>
    public double StaleThresholdDays
    {
        get => Settings.StaleThresholdDays;
        set
        {
            var days = double.IsNaN(value) ? AuditSettings.DefaultStaleThresholdDays : (int)Math.Round(value);
            Settings.StaleThresholdDays = Math.Clamp(days, AuditSettings.MinStaleThresholdDays, AuditSettings.MaxStaleThresholdDays);
            OnPropertyChanged();
        }
    }

    public string? RegexProblem =>
        Settings.NamingConventionIsValid ? null : "Invalid regex: the naming check is off until it's fixed.";

    public bool HasRegexProblem => RegexProblem is not null;

    public bool HasConfig => Session.Config is not null;

    public string TenantId => Session.Config?.TenantId ?? "";

    public string ClientId => Session.Config?.ClientId ?? "";

    public bool HasPermissions => Permissions is not null;

    public string TenantText =>
        Session.User is { } user ? user.TenantName ?? user.TenantId : Session.IsDemo ? "Demo data" : "";

    /// <summary>Stops following the settings once the page is gone.</summary>
    public void Detach() => Settings.PropertyChanged -= SettingsChanged;

    [RelayCommand]
    private Task SignOutAsync() => Session.SignOutAsync();

    /// <summary>Signs out first, so the old tenant's account isn't left behind.</summary>
    [RelayCommand]
    private Task ChangeTenantSetupAsync() => Session.BeginSetupAsync();

    [RelayCommand]
    private void ClearAllLocalData()
    {
        try
        {
            LocalDataCleaner.ClearAll(_store, Settings);
            ClearMessage = "Local data cleared.";
        }
        catch (Exception error) when (error is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            ClearMessage = "Couldn't clear everything. Try again.";
        }
    }

    private void SettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsStore.StaleThresholdDays)) OnPropertyChanged(nameof(StaleThresholdDays));
        if (e.PropertyName == nameof(SettingsStore.NamingConventionRegex))
        {
            OnPropertyChanged(nameof(RegexProblem));
            OnPropertyChanged(nameof(HasRegexProblem));
        }
    }
}
