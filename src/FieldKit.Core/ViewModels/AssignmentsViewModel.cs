using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// A device's or person's groups, and which Intune apps, profiles, policies and scripts are aimed
/// at them through those groups (ports <c>AssignmentsModel</c>).
/// </summary>
public sealed partial class AssignmentsViewModel : ObservableObject
{
    private readonly DirectoryLookup _directory;
    private readonly AssignmentCatalog _catalog;
    private readonly Action _onUnauthorized;
    private readonly Dictionary<AssignmentKind, SectionState<IReadOnlyList<AssignedItem>>> _catalogStates = [];
    private CancellationTokenSource _cancellation = new();

    public AssignmentsViewModel(AssignmentSubject subject, DirectoryLookup directory, AssignmentCatalog catalog, Action onUnauthorized)
    {
        Subject = subject;
        _directory = directory;
        _catalog = catalog;
        _onUnauthorized = onUnauthorized;
        switch (subject)
        {
            case AssignmentSubject.Device device:
                DeviceGroups = device.EntraObjectId is null ? null : new GroupListSection("Device groups");
                UserGroups = string.IsNullOrEmpty(device.PrimaryUserUpn) ? null : new GroupListSection($"Primary user's groups ({device.PrimaryUserUpn})");
                break;
            case AssignmentSubject.Person:
                UserGroups = new GroupListSection("Groups");
                break;
        }
        foreach (var kind in Enum.GetValues<AssignmentKind>())
        {
            _catalogStates[kind] = SectionState<IReadOnlyList<AssignedItem>>.Loading;
        }
        Sections = Enum.GetValues<AssignmentKind>()
            .Select(kind => new AssignmentKindSection(kind, "None targeted.") { RetryCommand = RefreshCommand })
            .ToList();
    }

    public AssignmentSubject Subject { get; }

    /// <summary>The device's own groups; null for a person, or a device without an Entra object.</summary>
    public GroupListSection? DeviceGroups { get; }

    /// <summary>The person's groups (or the device's primary user's); null when there's no user.</summary>
    public GroupListSection? UserGroups { get; }

    public bool HasDeviceGroups => DeviceGroups is not null;

    public bool HasUserGroups => UserGroups is not null;

    /// <summary>One per kind, in display order.</summary>
    public IReadOnlyList<AssignmentKindSection> Sections { get; }

    public string Explanation =>
        $"What Intune aims at {Subject.Title} through its groups. Assignment filters aren't evaluated, so this shows what's targeted, not proof it installed.";

    [RelayCommand]
    public Task LoadAsync() => LoadAllAsync(refresh: false);

    /// <summary>Reloads groups and refetches every catalog kind.</summary>
    [RelayCommand]
    public Task RefreshAsync() => LoadAllAsync(refresh: true);

    public void Cancel()
    {
        _cancellation.Cancel();
        _cancellation = new CancellationTokenSource();
    }

    private async Task LoadAllAsync(bool refresh)
    {
        var cancellationToken = _cancellation.Token;
        var tasks = new List<Task> { LoadGroupsAsync(cancellationToken) };
        tasks.AddRange(Enum.GetValues<AssignmentKind>().Select(kind => LoadCatalogAsync(kind, refresh, cancellationToken)));
        await Task.WhenAll(tasks);
    }

    private async Task LoadGroupsAsync(CancellationToken cancellationToken)
    {
        var device = Subject is AssignmentSubject.Device { EntraObjectId: { } objectId } && DeviceGroups is not null
            ? LoadGroupsAsync(DeviceGroups, $"devices/{objectId}", cancellationToken)
            : Task.CompletedTask;
        var userPath = Subject switch
        {
            AssignmentSubject.Device { PrimaryUserUpn: { Length: > 0 } upn } => $"users/{upn}",
            AssignmentSubject.Person person => $"users/{person.User.Id}",
            _ => null,
        };
        var user = userPath is not null && UserGroups is not null
            ? LoadGroupsAsync(UserGroups, userPath, cancellationToken)
            : Task.CompletedTask;
        await Task.WhenAll(device, user);
    }

    private async Task LoadGroupsAsync(GroupListSection section, string path, CancellationToken cancellationToken)
    {
        section.State = SectionState<IReadOnlyList<DirectoryGroup>>.Loading;
        Resolve();
        try
        {
            var groups = await _directory.GroupsOfAsync(path, cancellationToken: cancellationToken);
            section.State = groups.Count == 0
                ? SectionState<IReadOnlyList<DirectoryGroup>>.NotFound
                : SectionState<IReadOnlyList<DirectoryGroup>>.Loaded(groups);
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.Groups) is not { } message) return;
            section.State = SectionState<IReadOnlyList<DirectoryGroup>>.Failed(message);
        }
        Resolve();
    }

    private async Task LoadCatalogAsync(AssignmentKind kind, bool refresh, CancellationToken cancellationToken)
    {
        _catalogStates[kind] = SectionState<IReadOnlyList<AssignedItem>>.Loading;
        Resolve();
        if (await CatalogLoader.LoadAsync(kind, _catalog, refresh, _onUnauthorized, cancellationToken) is { } state)
        {
            _catalogStates[kind] = state;
        }
        Resolve();
    }

    /// <summary>What applies for each kind, once groups and that kind's catalog have loaded.</summary>
    private void Resolve()
    {
        var groupsLoading = DeviceGroups?.State.IsLoading == true || UserGroups?.State.IsLoading == true;
        var membership = Membership();
        foreach (var section in Sections)
        {
            var catalog = _catalogStates[section.Kind];
            section.State = AssignmentKindSection.Unresolved(catalog)
                ?? (groupsLoading
                    ? SectionState<IReadOnlyList<ResolvedAssignment>>.Loading
                    : AssignmentKindSection.FromResolved(AssignmentResolver.Resolve(catalog.Value!, membership)));
        }
    }

    private AssignmentResolver.Membership Membership()
    {
        var names = new Dictionary<string, string>();
        foreach (var section in new[] { DeviceGroups, UserGroups })
        {
            if (section is null) continue;
            foreach (var group in section.Groups) names[group.Id] = group.DisplayName;
        }
        return new AssignmentResolver.Membership(
            names,
            IncludesAllDevices: Subject is AssignmentSubject.Device,
            IncludesAllUsers: UserGroups is not null);
    }
}
