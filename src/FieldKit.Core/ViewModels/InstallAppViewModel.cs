using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Installs an app on a device by adding the device (or its primary user) to a group the app is
/// assigned to as Required (ports <c>InstallAppModel</c>).
/// </summary>
public sealed partial class InstallAppViewModel : ObservableObject
{
    private readonly AssignmentCatalog _catalog;
    private readonly DirectoryLookup _directory;
    private readonly GroupMembershipUpdates _membership;
    private readonly DeviceActions _actions;
    private readonly Action _onUnauthorized;
    private readonly Dictionary<string, SectionState<IReadOnlyList<InstallOption>>> _options = [];

    /// <summary>Group ID to name, including nested membership.</summary>
    private Dictionary<string, string> _deviceGroups = [];
    private Dictionary<string, string> _userGroups = [];

    /// <summary>Groups the device or user is directly in: the only ones they can be removed from.</summary>
    private HashSet<string> _directDeviceGroups = [];
    private HashSet<string> _directUserGroups = [];
    private DirectoryUser? _user;

    public InstallAppViewModel(
        IntuneDevice device,
        string? entraObjectId,
        AssignmentCatalog catalog,
        DirectoryLookup directory,
        GroupMembershipUpdates membership,
        DeviceActions actions,
        Action onUnauthorized)
    {
        Device = device;
        EntraObjectId = entraObjectId;
        _catalog = catalog;
        _directory = directory;
        _membership = membership;
        _actions = actions;
        _onUnauthorized = onUnauthorized;
        Apps = SectionState<IReadOnlyList<AssignedItem>>.Loading;
        Outcome = InstallOutcome.Idle;
        OptionRows = SectionState<IReadOnlyList<InstallOptionRow>>.Loading;
    }

    public IntuneDevice Device { get; }

    public string? EntraObjectId { get; }

    public bool HasUser => Device.UserPrincipalName.Length > 0;

    /// <summary>"Alex Doe", or the UPN, or "user".</summary>
    public string UserName =>
        Device.UserDisplayName.Length > 0 ? Device.UserDisplayName
        : Device.UserPrincipalName.Length > 0 ? Device.UserPrincipalName
        : "user";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppRows))]
    public partial SectionState<IReadOnlyList<AssignedItem>> Apps { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppRows))]
    public partial string Query { get; set; } = "";

    /// <summary>Apps for this platform matching the search, with their current status.</summary>
    public IReadOnlyList<InstallAppRow> AppRows
    {
        get
        {
            if (Apps.Kind != SectionKind.Loaded) return [];
            var term = Query.Trim();
            return Apps.Value!
                .Where(app => term.Length == 0 || app.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                .Select(app => new InstallAppRow(app, StatusOf(app)))
                .ToList();
        }
    }

    public string AppsNotFoundText => $"No Intune apps for {Device.OperatingSystem}.";

    public string AppsFooter => $"{Device.OperatingSystem} apps in Intune. Apps install when they're assigned as Required to a group the device or its user is in.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutcomeText), nameof(IsWorking), nameof(OutcomeIsSuccess), nameof(OutcomeIsFailure), nameof(HasOutcome))]
    public partial InstallOutcome Outcome { get; private set; }

    public bool IsWorking => Outcome is InstallOutcome.WorkingOutcome;

    public bool HasOutcome => Outcome is not InstallOutcome.IdleOutcome;

    public bool OutcomeIsSuccess => Outcome is InstallOutcome.Added;

    public bool OutcomeIsFailure => Outcome is InstallOutcome.Failed;

    public string OutcomeText => Outcome switch
    {
        InstallOutcome.WorkingOutcome => "Working…",
        InstallOutcome.Added added => added.Text,
        InstallOutcome.Failed failed => failed.Text,
        _ => "",
    };

    // The app picked for the options view.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedApp), nameof(SelectedName), nameof(SelectedDetail), nameof(SelectedStatusText), nameof(SelectedStatusKind), nameof(NoGroupText))]
    public partial AssignedItem? SelectedApp { get; private set; }

    public bool HasSelectedApp => SelectedApp is not null;

    public string SelectedName => SelectedApp?.Name ?? "";

    public string SelectedDetail => SelectedApp?.Detail ?? "";

    public AppInstallPlan.StatusKind SelectedStatusKind => SelectedApp is { } app ? StatusOf(app).Kind : AppInstallPlan.StatusKind.NotRequired;

    public string SelectedStatusText
    {
        get
        {
            if (SelectedApp is not { } app) return "";
            var status = StatusOf(app);
            return status.Kind switch
            {
                AppInstallPlan.StatusKind.Required => $"Already required via {string.Join(", ", status.Via)}. If it isn't installed, try Sync.",
                AppInstallPlan.StatusKind.Excluded => $"Excluded via {string.Join(", ", status.Via)}. Joining a Required group won't help until the exclusion is removed in Intune.",
                _ => "Not required for this device yet.",
            };
        }
    }

    /// <summary>The selected app's Required groups as rows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OptionRowList))]
    public partial SectionState<IReadOnlyList<InstallOptionRow>> OptionRows { get; private set; }

    public IReadOnlyList<InstallOptionRow> OptionRowList => OptionRows.Kind == SectionKind.Loaded ? OptionRows.Value! : [];

    /// <summary>What to say when no group would install the selected app.</summary>
    public string NoGroupText
    {
        get
        {
            if (SelectedApp is not { } app) return "";
            var (devices, users) = AppInstallPlan.RequiredForAll(app);
            return devices || users
                ? $"Required for all {(devices ? "devices" : "users")}, so there's no group to join. If it isn't installed, try Sync."
                : $"This app isn't assigned as Required to any group. Ask an Intune admin to assign it as Required to an assigned group (for example \"APP-{app.Name}-Install\"), then come back.";
        }
    }

    public string OptionsFooter =>
        "Joining any assigned group below makes Intune install the app. Dynamic groups fill themselves from a rule, so FieldKit can't add to them.";

    public async Task LoadAsync(bool refresh = false)
    {
        var memberships = LoadMembershipsAsync();
        await LoadAppsAsync(refresh);
        await memberships;
    }

    public AppInstallPlan.Status StatusOf(AssignedItem app) =>
        AppInstallPlan.StatusOf(app, _deviceGroups, _userGroups, HasUser);

    public bool IsMember(InstallMember member, DirectoryGroup group) =>
        member == InstallMember.Device ? _deviceGroups.ContainsKey(group.Id) : _userGroups.ContainsKey(group.Id);

    /// <summary>Direct members can be removed; members through a nested group can't.</summary>
    public bool IsDirectMember(InstallMember member, DirectoryGroup group) =>
        member == InstallMember.Device ? _directDeviceGroups.Contains(group.Id) : _directUserGroups.Contains(group.Id);

    public SectionState<IReadOnlyList<InstallOption>> Options(AssignedItem app) =>
        _options.GetValueOrDefault(app.Id) ?? SectionState<IReadOnlyList<InstallOption>>.Loading;

    /// <summary>Shows an app's options (and loads them).</summary>
    public Task SelectAsync(AssignedItem app)
    {
        SelectedApp = app;
        Outcome = InstallOutcome.Idle;
        return LoadOptionsAsync(app);
    }

    /// <summary>Back to the app list.</summary>
    public void ClearSelection()
    {
        SelectedApp = null;
        Outcome = InstallOutcome.Idle;
    }

    /// <summary>The Required groups for an app, with what else each one targets.</summary>
    public async Task LoadOptionsAsync(AssignedItem app)
    {
        var groupIds = AppInstallPlan.RequiredGroupIds(app);
        if (groupIds.Count == 0)
        {
            SetOptions(app, SectionState<IReadOnlyList<InstallOption>>.NotFound);
            return;
        }
        SetOptions(app, SectionState<IReadOnlyList<InstallOption>>.Loading);
        try
        {
            var everything = new List<AssignedItem>();
            foreach (var kind in Enum.GetValues<AssignmentKind>())
            {
                // Best effort: a kind that fails just isn't listed as a side effect.
                try
                {
                    everything.AddRange(await _catalog.ItemsAsync(kind));
                }
                catch (GraphException error) when (error.Kind != GraphErrorKind.Unauthorized)
                {
                    Debug.WriteLine($"Catalog {kind} unavailable for side effects: {error.Kind}");
                }
            }
            var found = new List<InstallOption>();
            foreach (var id in groupIds)
            {
                var group = await _directory.GroupAsync(id);
                var others = AssignmentResolver.Targeting(id, group.DisplayName, everything)
                    .Where(resolved => resolved.Item.Id != app.Id && !resolved.IsExcluded)
                    .Select(resolved => resolved.Item.Name)
                    .ToList();
                found.Add(new InstallOption(group, others));
            }
            // Assigned groups first: they're the ones FieldKit can add to.
            SetOptions(app, SectionState<IReadOnlyList<InstallOption>>.Loaded(found.OrderBy(option => option.Group.IsDynamic).ToList()));
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.Groups) is not { } message) return;
            SetOptions(app, SectionState<IReadOnlyList<InstallOption>>.Failed(message));
        }
    }

    /// <summary>Adds the device or user to the group. Returns true on success.</summary>
    public async Task<bool> AddAsync(InstallMember member, DirectoryGroup group, AssignedItem app)
    {
        Outcome = InstallOutcome.Working;
        try
        {
            string objectId, name;
            if (member == InstallMember.Device)
            {
                if (EntraObjectId is null)
                {
                    Outcome = new InstallOutcome.Failed("This device has no Entra object, so it can't join a group.");
                    return false;
                }
                (objectId, name) = (EntraObjectId, Device.DeviceName);
            }
            else
            {
                var user = await ResolvedUserAsync();
                (objectId, name) = (user.Id, user.DisplayName);
            }
            await _membership.AddMemberAsync(objectId, group.Id);
            Debug.WriteLine("Added member to an app install group");
            if (member == InstallMember.Device)
            {
                _deviceGroups[group.Id] = group.DisplayName;
                _directDeviceGroups.Add(group.Id);
            }
            else
            {
                _userGroups[group.Id] = group.DisplayName;
                _directUserGroups.Add(group.Id);
            }
            MembershipsChanged();
            Outcome = new InstallOutcome.Added($"Added {name} to {group.DisplayName}. Intune installs {app.Name} once the device picks up the change, usually within minutes. Sync now to speed it up.");
            return true;
        }
        catch (Exception error)
        {
            Outcome = FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AppInstall) is { } message
                ? new InstallOutcome.Failed(message)
                : InstallOutcome.Idle;
            return false;
        }
    }

    /// <summary>Removes the device or user from the group. Returns true on success.</summary>
    public async Task<bool> RemoveAsync(InstallMember member, DirectoryGroup group)
    {
        Outcome = InstallOutcome.Working;
        try
        {
            string objectId, name;
            if (member == InstallMember.Device)
            {
                if (EntraObjectId is null)
                {
                    Outcome = new InstallOutcome.Failed("This device has no Entra object.");
                    return false;
                }
                (objectId, name) = (EntraObjectId, Device.DeviceName);
            }
            else
            {
                var user = await ResolvedUserAsync();
                (objectId, name) = (user.Id, user.DisplayName);
            }
            await _membership.RemoveMemberAsync(objectId, group.Id);
            Debug.WriteLine("Removed member from a group");
            if (member == InstallMember.Device)
            {
                _directDeviceGroups.Remove(group.Id);
                _deviceGroups.Remove(group.Id);
            }
            else
            {
                _directUserGroups.Remove(group.Id);
                _userGroups.Remove(group.Id);
            }
            MembershipsChanged();
            Outcome = new InstallOutcome.Added($"Removed {name} from {group.DisplayName}. What the group targets stops applying once Intune picks up the change.");
            // Nested memberships may still include the group; reload to stay accurate.
            await LoadMembershipsAsync();
            return true;
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            Outcome = new InstallOutcome.Failed($"Not a direct member of {group.DisplayName}; it may be in it through a nested group, which has to change instead.");
            return false;
        }
        catch (Exception error)
        {
            Outcome = FeatureErrors.Message(error, _onUnauthorized, GraphFeature.GroupMembership) is { } message
                ? new InstallOutcome.Failed(message)
                : InstallOutcome.Idle;
            return false;
        }
    }

    public async Task SyncDeviceAsync()
    {
        Outcome = InstallOutcome.Working;
        try
        {
            await _actions.PerformAsync(DeviceAction.Sync, Device.Id);
            Outcome = new InstallOutcome.Added(DeviceAction.Sync.SuccessMessage());
        }
        catch (Exception error)
        {
            Outcome = FeatureErrors.Message(error, _onUnauthorized, GraphFeature.DeviceActions) is { } message
                ? new InstallOutcome.Failed(message)
                : InstallOutcome.Idle;
        }
    }

    public void ClearOutcome() => Outcome = InstallOutcome.Idle;

    /// <summary>"A, B, C and 2 more".</summary>
    public static string Summary(IReadOnlyList<string> names)
    {
        var shown = string.Join(", ", names.Take(3));
        return names.Count > 3 ? $"{shown} and {names.Count - 3} more" : shown;
    }

    public string AddTitle(InstallMember member, DirectoryGroup group) =>
        $"Add {(member == InstallMember.Device ? Device.DeviceName : UserName)} to {group.DisplayName}?";

    public string AddMessage(InstallOption option, AssignedItem app) =>
        option.AlsoTargets.Count == 0
            ? $"Intune then installs {app.Name}."
            : $"Intune then installs {app.Name}. This group also brings in {Summary(option.AlsoTargets)}.";

    public string RemoveTitle(InstallMember member, DirectoryGroup group) =>
        $"Remove {(member == InstallMember.Device ? Device.DeviceName : UserName)} from {group.DisplayName}?";

    private async Task LoadAppsAsync(bool refresh)
    {
        var platform = AppPlatformParser.FromOperatingSystem(Device.OperatingSystem);
        try
        {
            var all = await _catalog.ItemsAsync(AssignmentKind.App, refresh);
            var fitting = all
                .Where(app => platform is null || app.Platform == platform)
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            Apps = fitting.Count == 0
                ? SectionState<IReadOnlyList<AssignedItem>>.NotFound
                : SectionState<IReadOnlyList<AssignedItem>>.Loaded(fitting);
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AppAssignments) is { } message)
            {
                Apps = SectionState<IReadOnlyList<AssignedItem>>.Failed(message);
            }
        }
    }

    private async Task LoadMembershipsAsync()
    {
        if (EntraObjectId is { } objectId)
        {
            (_deviceGroups, _directDeviceGroups) = await GroupsAsync($"devices/{objectId}");
        }
        if (HasUser)
        {
            (_userGroups, _directUserGroups) = await GroupsAsync($"users/{Device.UserPrincipalName}");
        }
        MembershipsChanged();
    }

    /// <summary>All groups (with names) and direct ones; missing on failure, as iOS's <c>try?</c>.</summary>
    private async Task<(Dictionary<string, string> All, HashSet<string> Direct)> GroupsAsync(string path)
    {
        var direct = TryGroupsAsync(path, transitive: false);
        var all = await TryGroupsAsync(path, transitive: true);
        var names = new Dictionary<string, string>();
        foreach (var group in all) names.TryAdd(group.Id, group.DisplayName);
        return (names, (await direct).Select(group => group.Id).ToHashSet());
    }

    private async Task<IReadOnlyList<DirectoryGroup>> TryGroupsAsync(string path, bool transitive)
    {
        try
        {
            return await _directory.GroupsOfAsync(path, transitive);
        }
        catch (GraphException)
        {
            return [];
        }
    }

    private async Task<DirectoryUser> ResolvedUserAsync() => _user ??= await _directory.UserAsync(Device.UserPrincipalName);

    private void SetOptions(AssignedItem app, SectionState<IReadOnlyList<InstallOption>> state)
    {
        _options[app.Id] = state;
        if (SelectedApp?.Id != app.Id) return;
        OptionRows = state.Kind switch
        {
            SectionKind.Loaded => SectionState<IReadOnlyList<InstallOptionRow>>.Loaded(state.Value!.Select(option => new InstallOptionRow(option, this)).ToList()),
            SectionKind.Failed => SectionState<IReadOnlyList<InstallOptionRow>>.Failed(state.Message ?? ""),
            SectionKind.NotFound or SectionKind.Skipped => SectionState<IReadOnlyList<InstallOptionRow>>.NotFound,
            _ => SectionState<IReadOnlyList<InstallOptionRow>>.Loading,
        };
    }

    private void MembershipsChanged()
    {
        OnPropertyChanged(nameof(AppRows));
        OnPropertyChanged(nameof(SelectedStatusText));
        OnPropertyChanged(nameof(SelectedStatusKind));
        if (OptionRows.Kind == SectionKind.Loaded)
        {
            foreach (var row in OptionRows.Value!) row.Refresh();
        }
    }
}
