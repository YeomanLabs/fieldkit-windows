using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Finds devices by name, people by name, UPN or mail, and groups by name (ports
/// <c>SearchModel</c>). Each search loads and fails on its own; typing cancels the previous one.
/// The Autopilot user picker reuses it with devices and groups turned off.
/// </summary>
public sealed partial class SearchViewModel : ObservableObject
{
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(350);

    private readonly DeviceLookup _lookup;
    private readonly DirectoryLookup _directory;
    private readonly bool _includeDevices;
    private readonly bool _includeGroups;
    private readonly Action _onUnauthorized;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private CancellationTokenSource? _current;

    public SearchViewModel(
        DeviceLookup lookup,
        DirectoryLookup directory,
        Action onUnauthorized,
        bool includeDevices = true,
        bool includeGroups = true,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _lookup = lookup;
        _directory = directory;
        _onUnauthorized = onUnauthorized;
        _includeDevices = includeDevices;
        _includeGroups = includeGroups;
        _delay = delay ?? Task.Delay;
        Query = "";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SerialSuggestion), nameof(HasSerialSuggestion), nameof(SerialSuggestionText), nameof(ShowsHint))]
    public partial string Query { get; set; }

    /// <summary>Null until a search runs (the column is hidden).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceResults), nameof(ShowsDevices))]
    public partial SectionState<IReadOnlyList<IntuneDevice>>? Devices { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeopleResults), nameof(ShowsPeople))]
    public partial SectionState<IReadOnlyList<DirectoryUser>>? People { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupResults), nameof(ShowsGroups))]
    public partial SectionState<IReadOnlyList<DirectoryGroup>>? Groups { get; private set; }

    public IReadOnlyList<IntuneDevice> DeviceResults => Devices?.Kind == SectionKind.Loaded ? Devices.Value! : [];

    public IReadOnlyList<DirectoryUser> PeopleResults => People?.Kind == SectionKind.Loaded ? People.Value! : [];

    public IReadOnlyList<DirectoryGroup> GroupResults => Groups?.Kind == SectionKind.Loaded ? Groups.Value! : [];

    /// <summary>A query that reads as a serial can open the device directly.</summary>
    public string? SerialSuggestion => SerialNormalizer.Normalize(Query);

    public bool HasSerialSuggestion => SerialSuggestion is not null;

    public string SerialSuggestionText => SerialSuggestion is { } serial ? $"Look up serial {serial}" : "";

    public bool ShowsHint => Query.Trim().Length < 2;

    public bool ShowsDevices => Devices is not null;

    public bool ShowsPeople => People is not null;

    public bool ShowsGroups => Groups is not null;

    /// <summary>The search started by the latest change to <see cref="Query"/>, for tests.</summary>
    public Task CurrentSearch { get; private set; } = Task.CompletedTask;

    public async Task SearchAsync(string query)
    {
        _current?.Cancel();
        var current = new CancellationTokenSource();
        _current = current;
        var cancellationToken = current.Token;

        var term = query.Trim();
        if (term.Length < 2)
        {
            Devices = null;
            People = null;
            Groups = null;
            return;
        }
        try
        {
            await _delay(Debounce, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cancellationToken.IsCancellationRequested) return;

        if (_includeDevices) Devices = SectionState<IReadOnlyList<IntuneDevice>>.Loading;
        if (_includeGroups) Groups = SectionState<IReadOnlyList<DirectoryGroup>>.Loading;
        People = SectionState<IReadOnlyList<DirectoryUser>>.Loading;

        var searches = new List<Task> { SearchPeopleAsync(term, cancellationToken) };
        if (_includeDevices) searches.Add(SearchDevicesAsync(term, cancellationToken));
        if (_includeGroups) searches.Add(SearchGroupsAsync(term, cancellationToken));
        await Task.WhenAll(searches);
    }

    [RelayCommand]
    private Task RetryAsync() => SearchAsync(Query);

    partial void OnQueryChanged(string value) => CurrentSearch = SearchAsync(value);

    private async Task SearchDevicesAsync(string term, CancellationToken cancellationToken)
    {
        try
        {
            var found = await _lookup.SearchDevicesAsync(term, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            Devices = found.Count == 0 ? SectionState<IReadOnlyList<IntuneDevice>>.NotFound : SectionState<IReadOnlyList<IntuneDevice>>.Loaded(found);
        }
        catch (Exception error)
        {
            if (cancellationToken.IsCancellationRequested || FeatureErrors.Message(error, _onUnauthorized) is not { } message) return;
            Devices = SectionState<IReadOnlyList<IntuneDevice>>.Failed(message);
        }
    }

    private async Task SearchPeopleAsync(string term, CancellationToken cancellationToken)
    {
        try
        {
            var found = await _directory.SearchUsersAsync(term, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            People = found.Count == 0 ? SectionState<IReadOnlyList<DirectoryUser>>.NotFound : SectionState<IReadOnlyList<DirectoryUser>>.Loaded(found);
        }
        catch (Exception error)
        {
            if (cancellationToken.IsCancellationRequested || FeatureErrors.Message(error, _onUnauthorized, GraphFeature.PeopleSearch) is not { } message) return;
            People = SectionState<IReadOnlyList<DirectoryUser>>.Failed(message);
        }
    }

    private async Task SearchGroupsAsync(string term, CancellationToken cancellationToken)
    {
        try
        {
            var found = await _directory.SearchGroupsAsync(term, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            Groups = found.Count == 0 ? SectionState<IReadOnlyList<DirectoryGroup>>.NotFound : SectionState<IReadOnlyList<DirectoryGroup>>.Loaded(found);
        }
        catch (Exception error)
        {
            if (cancellationToken.IsCancellationRequested || FeatureErrors.Message(error, _onUnauthorized, GraphFeature.Groups) is not { } message) return;
            Groups = SectionState<IReadOnlyList<DirectoryGroup>>.Failed(message);
        }
    }
}
