using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>The Users tab: every user A–Z, or people whose name, UPN or email starts with what's typed.</summary>
public sealed class UserListViewModel(DirectoryLookup directory, Action onUnauthorized, Func<TimeSpan, CancellationToken, Task>? delay = null)
    : BrowseViewModel<DirectoryUser>(onUnauthorized, delay)
{
    public override string EmptyText => "No users in this directory.";

    public override string NoMatchText => "No people match.";

    protected override string AllHeading => "All users";

    protected override GraphFeature? Feature => GraphFeature.PeopleSearch;

    protected override Task<GraphPage<DirectoryUser>> FirstPageAsync(CancellationToken cancellationToken) =>
        directory.UsersPageAsync(cancellationToken: cancellationToken);

    protected override Task<GraphPage<DirectoryUser>> NextPageAsync(Uri next, CancellationToken cancellationToken) =>
        directory.UsersPageAsync(next, cancellationToken);

    protected override Task<IReadOnlyList<DirectoryUser>> SearchAsync(string term, CancellationToken cancellationToken) =>
        directory.SearchUsersAsync(term, SearchLimit, cancellationToken);

    protected override string SearchHeading(string term) => $"People matching “{term}”";
}
