using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// A tenant-wide list for the Devices, Users and Groups tabs (Windows only; replaces Lookup and
/// Search, the user's decision on 2026-10-13). It fills with the first page A–Z as soon as it's
/// shown and loads more on request; typing two or more characters searches Graph instead, after
/// a short pause, and clearing the box puts back the pages already listed without asking Graph again.
/// </summary>
public abstract partial class BrowseViewModel<T> : ObservableObject where T : class
{
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(350);

    /// <summary>How many search results to ask Graph for.</summary>
    public const int SearchLimit = 50;

    private readonly Action _onUnauthorized;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private CancellationTokenSource _current = new();
    private Uri? _next;

    /// <summary>The pages listed so far while browsing, kept while a search is shown.</summary>
    private List<T>? _browsed;

    /// <summary>Where browsing continues after <see cref="_browsed"/>.</summary>
    private Uri? _browsedNext;

    /// <summary>The load <see cref="StartOverAsync"/> started, so a second request while it runs joins it.</summary>
    private Task? _startOver;

    /// <summary>Set while <see cref="StartOverAsync"/> empties the box, so that doesn't start a load of its own.</summary>
    private bool _clearingForStartOver;

    protected BrowseViewModel(Action onUnauthorized, Func<TimeSpan, CancellationToken, Task>? delay)
    {
        _onUnauthorized = onUnauthorized;
        _delay = delay ?? Task.Delay;
        State = SectionState<int>.Loading;
    }

    /// <summary>What's shown: the browsed pages so far, or the search results.</summary>
    public ObservableCollection<T> Items { get; } = [];

    /// <summary>The list as a whole: loading, loaded (with a count), nothing found, or failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(IsBusy))]
    public partial SectionState<int> State { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearching), nameof(Heading), nameof(CanLoadMore))]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore), nameof(IsBusy))]
    public partial bool IsLoadingMore { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    public partial bool HasMore { get; private set; }

    /// <summary>The page after the last one shown can be fetched (browsing, not searching).</summary>
    public bool CanLoadMore => HasMore && !IsSearching && !IsLoadingMore;

    /// <summary>Why the last "load more" failed; the rows already shown stay.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadMoreError))]
    public partial string? LoadMoreError { get; private set; }

    public bool HasLoadMoreError => LoadMoreError is not null;

    public bool IsSearching => Query.Trim().Length >= 2;

    public bool IsBusy => State.IsLoading || IsLoadingMore;

    /// <summary>"All devices (100 so far)", "All devices (37)" or "Devices matching “cts” (12)".</summary>
    public string Heading
    {
        get
        {
            if (IsSearching)
            {
                return State.Kind == SectionKind.Loaded ? $"{SearchHeading(Query.Trim())} ({Items.Count})" : SearchHeading(Query.Trim());
            }
            return State.Kind == SectionKind.Loaded
                ? HasMore ? $"{AllHeading} ({Items.Count} so far)" : $"{AllHeading} ({Items.Count})"
                : AllHeading;
        }
    }

    /// <summary>Shown when browsing finds nothing.</summary>
    public abstract string EmptyText { get; }

    /// <summary>Shown when a search finds nothing.</summary>
    public abstract string NoMatchText { get; }

    public string NotFoundText => IsSearching ? NoMatchText : EmptyText;

    /// <summary>The load that the latest change to <see cref="Query"/> (or a refresh) started, for tests.</summary>
    public Task CurrentLoad { get; private set; } = Task.CompletedTask;

    protected abstract string AllHeading { get; }

    protected abstract GraphFeature? Feature { get; }

    /// <summary>Loads the first page the first time the tab is shown; later visits keep what's loaded.</summary>
    public Task EnsureLoadedAsync() =>
        _browsed is not null || IsSearching || !CurrentLoad.IsCompleted ? Task.CompletedTask : CurrentLoad = ReloadAsync();

    /// <summary>Starts over: the first page, or the search again.</summary>
    [RelayCommand]
    public Task RefreshAsync() => CurrentLoad = ReloadAsync(refresh: true);

    /// <summary>
    /// The tab's menu item was chosen (Windows, the user's request on 2026-10-14): an empty box and
    /// the first page fresh from Graph, whatever was searched or scrolled before. Coming back to the
    /// tab with Back leaves it as it is instead (<see cref="EnsureLoadedAsync"/>).
    /// </summary>
    public Task StartOverAsync()
    {
        if (Query.Length == 0 && _startOver is { IsCompleted: false } running) return running;
        _clearingForStartOver = true;
        try
        {
            Query = "";
        }
        finally
        {
            _clearingForStartOver = false;
        }
        return _startOver = CurrentLoad = ReloadAsync(refresh: true);
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (!CanLoadMore || _next is not { } next) return;
        var cancellationToken = _current.Token;
        IsLoadingMore = true;
        LoadMoreError = null;
        try
        {
            var page = await NextPageAsync(next, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            foreach (var item in page.Items) Items.Add(item);
            _browsed?.AddRange(page.Items);
            Accept(page);
        }
        catch (Exception error)
        {
            if (cancellationToken.IsCancellationRequested) return;
            // Keep what's shown; say why the next page didn't come.
            if (FeatureErrors.Message(error, _onUnauthorized, Feature) is { } message) LoadMoreError = message;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    protected abstract Task<GraphPage<T>> FirstPageAsync(CancellationToken cancellationToken);

    protected abstract Task<GraphPage<T>> NextPageAsync(Uri next, CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<T>> SearchAsync(string term, CancellationToken cancellationToken);

    protected abstract string SearchHeading(string term);

    /// <summary>Called when the text changes, before the list reloads.</summary>
    protected virtual void OnQueryEdited()
    {
    }

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(NotFoundText));
        OnQueryEdited();
        if (!_clearingForStartOver) CurrentLoad = ReloadAsync(debounce: true);
    }

    private async Task ReloadAsync(bool debounce = false, bool refresh = false)
    {
        _current.Cancel();
        var current = new CancellationTokenSource();
        _current = current;
        var cancellationToken = current.Token;
        var term = Query.Trim();
        var searching = term.Length >= 2;

        if (debounce && searching)
        {
            try
            {
                await _delay(Debounce, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        if (cancellationToken.IsCancellationRequested) return;

        Items.Clear();
        LoadMoreError = null;
        if (!searching && !refresh && _browsed is { } browsed)
        {
            // Back from a search: the pages already listed, ready to continue.
            foreach (var item in browsed) Items.Add(item);
            Accept(new GraphPage<T>(browsed, _browsedNext));
            return;
        }

        State = SectionState<int>.Loading;
        HasMore = false;
        _next = null;
        if (!searching) _browsed = null;
        try
        {
            if (searching)
            {
                var found = await SearchAsync(term, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
                foreach (var item in found) Items.Add(item);
                State = found.Count == 0 ? SectionState<int>.NotFound : SectionState<int>.Loaded(found.Count);
            }
            else
            {
                var page = await FirstPageAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
                foreach (var item in page.Items) Items.Add(item);
                _browsed = [.. page.Items];
                Accept(page);
            }
        }
        catch (Exception error)
        {
            if (cancellationToken.IsCancellationRequested) return;
            if (FeatureErrors.Message(error, _onUnauthorized, Feature) is { } message) State = SectionState<int>.Failed(message);
        }
    }

    private void Accept(GraphPage<T> page)
    {
        _next = page.NextLink;
        if (!IsSearching) _browsedNext = page.NextLink;
        HasMore = page.HasMore;
        State = Items.Count == 0 ? SectionState<int>.NotFound : SectionState<int>.Loaded(Items.Count);
        OnPropertyChanged(nameof(Heading));
    }
}
