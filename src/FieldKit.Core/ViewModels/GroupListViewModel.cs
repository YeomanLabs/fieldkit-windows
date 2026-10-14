using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>The Groups tab: every group A–Z, or groups whose name starts with what's typed.</summary>
public sealed class GroupListViewModel(DirectoryLookup directory, Action onUnauthorized, Func<TimeSpan, CancellationToken, Task>? delay = null)
    : BrowseViewModel<DirectoryGroup>(onUnauthorized, delay)
{
    public override string EmptyText => "No groups in this directory.";

    public override string NoMatchText => "No group names start with that.";

    protected override string AllHeading => "All groups";

    protected override GraphFeature? Feature => GraphFeature.Groups;

    protected override Task<GraphPage<DirectoryGroup>> FirstPageAsync(CancellationToken cancellationToken) =>
        directory.GroupsPageAsync(cancellationToken: cancellationToken);

    protected override Task<GraphPage<DirectoryGroup>> NextPageAsync(Uri next, CancellationToken cancellationToken) =>
        directory.GroupsPageAsync(next, cancellationToken);

    protected override Task<IReadOnlyList<DirectoryGroup>> SearchAsync(string term, CancellationToken cancellationToken) =>
        directory.SearchGroupsAsync(term, SearchLimit, cancellationToken);

    protected override string SearchHeading(string term) => $"Groups matching “{term}”";
}
