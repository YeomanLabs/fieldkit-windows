using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// The device page's install status tab (Windows only, 2026-10-14): did each targeted app
/// install, why not, and how each configuration policy went. Apps and configuration load and
/// fail on their own (both are beta).
/// </summary>
public sealed partial class InstallStatusViewModel : ObservableObject
{
    private readonly InstallStatusLookup _lookup;
    private readonly Action _onUnauthorized;
    private readonly TimeProvider _time;
    private CancellationTokenSource _cancellation = new();

    public InstallStatusViewModel(IntuneDevice device, InstallStatusLookup lookup, Action onUnauthorized, TimeProvider? time = null)
    {
        Device = device;
        _lookup = lookup;
        _onUnauthorized = onUnauthorized;
        _time = time ?? TimeProvider.System;
        Apps = SectionState<IReadOnlyList<AppStatusItem>>.Loading;
        Configuration = SectionState<IReadOnlyList<PolicyStatusItem>>.Loading;
    }

    public IntuneDevice Device { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppList), nameof(AppSummary))]
    public partial SectionState<IReadOnlyList<AppStatusItem>> Apps { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigurationList), nameof(ConfigurationSummary))]
    public partial SectionState<IReadOnlyList<PolicyStatusItem>> Configuration { get; private set; }

    /// <summary>Set when the apps loaded but their error codes couldn't be read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailureNote))]
    public partial string? FailureNote { get; private set; }

    public bool HasFailureNote => FailureNote is not null;

    public IReadOnlyList<AppStatusItem> AppList => Apps.Kind == SectionKind.Loaded ? Apps.Value! : [];

    public IReadOnlyList<PolicyStatusItem> ConfigurationList => Configuration.Kind == SectionKind.Loaded ? Configuration.Value! : [];

    /// <summary>"1 failed · 4 installed · 4 available".</summary>
    public string AppSummary => Summary(AppList.GroupBy(item => item.App.Outcome).OrderBy(group => group.Key).Select(group => group.Key switch
    {
        AppInstallOutcome.Failed => $"{group.Count()} failed",
        AppInstallOutcome.Pending => $"{group.Count()} pending",
        AppInstallOutcome.Missing => $"{group.Count()} not installed",
        AppInstallOutcome.NoStatus => $"{group.Count()} with no status yet",
        AppInstallOutcome.Installed => $"{group.Count()} installed",
        AppInstallOutcome.Removed => $"{group.Count()} uninstalled",
        AppInstallOutcome.Available => $"{group.Count()} available",
        _ => $"{group.Count()} not applicable",
    }));

    /// <summary>"1 error · 5 succeeded".</summary>
    public string ConfigurationSummary => Summary(ConfigurationList.GroupBy(item => item.Policy.State).OrderBy(group => group.Key)
        .Select(group => $"{group.Count()} {group.Key.DisplayName().ToLowerInvariant()}"));

    [RelayCommand]
    public Task LoadAsync()
    {
        _cancellation.Cancel();
        _cancellation = new CancellationTokenSource();
        return Task.WhenAll(LoadAppsAsync(_cancellation.Token), LoadConfigurationAsync(_cancellation.Token));
    }

    public void Cancel() => _cancellation.Cancel();

    private async Task LoadAppsAsync(CancellationToken cancellationToken)
    {
        Apps = SectionState<IReadOnlyList<AppStatusItem>>.Loading;
        FailureNote = null;
        try
        {
            var apps = (await _lookup.AppsAsync(Device, cancellationToken)).ToList();
            // Why the failed ones failed: one report request each, so only the first few.
            var failed = apps.Select((app, index) => (app, index)).Where(entry => entry.app.Outcome == AppInstallOutcome.Failed).Take(InstallStatusLookup.MaxFailureLookups).ToList();
            try
            {
                var failures = await Task.WhenAll(failed.Select(entry => _lookup.FailureAsync(entry.app, Device, cancellationToken)));
                for (var i = 0; i < failed.Count; i++) apps[failed[i].index] = apps[failed[i].index] with { Failure = failures[i] };
            }
            catch (GraphException error) when (error.Kind is not GraphErrorKind.Unauthorized)
            {
                FailureNote = $"FieldKit couldn't read why: {GraphFeature.AppInstallStatus.Message(error)}";
            }
            if (cancellationToken.IsCancellationRequested) return;
            var now = _time.GetUtcNow();
            Apps = apps.Count == 0
                ? SectionState<IReadOnlyList<AppStatusItem>>.NotFound
                : SectionState<IReadOnlyList<AppStatusItem>>.Loaded(apps
                    .OrderBy(app => app.Outcome)
                    .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(app => AppStatusItem.From(app, now)).ToList());
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AppInstallStatus) is not { } message) return;
            Apps = SectionState<IReadOnlyList<AppStatusItem>>.Failed(message);
        }
    }

    private async Task LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        Configuration = SectionState<IReadOnlyList<PolicyStatusItem>>.Loading;
        try
        {
            var policies = await _lookup.ConfigurationAsync(Device, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            var now = _time.GetUtcNow();
            Configuration = policies.Count == 0
                ? SectionState<IReadOnlyList<PolicyStatusItem>>.NotFound
                : SectionState<IReadOnlyList<PolicyStatusItem>>.Loaded(policies.Select(policy => PolicyStatusItem.From(policy, now)).ToList());
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.ConfigurationStatus) is not { } message) return;
            Configuration = SectionState<IReadOnlyList<PolicyStatusItem>>.Failed(message);
        }
    }

    private static string Summary(IEnumerable<string> parts) => string.Join(" · ", parts);
}
