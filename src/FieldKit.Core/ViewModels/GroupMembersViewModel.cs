using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>A group's direct members, and removing one (ports <c>GroupMembersModel</c>).</summary>
public sealed partial class GroupMembersViewModel : ObservableObject
{
    /// <summary>What removing a member does, shown before confirming (ports <c>GroupMembershipEffects</c>).</summary>
    public const string RemovalMessage =
        "Whatever this group targets stops applying once Intune picks up the change. Some profile settings can stay on the device until something else changes them, and apps aren't uninstalled unless an Uninstall assignment covers it.";

    private readonly DirectoryLookup _directory;
    private readonly GroupMembershipUpdates _updates;
    private readonly Action _onUnauthorized;

    public GroupMembersViewModel(DirectoryGroup group, DirectoryLookup directory, GroupMembershipUpdates updates, Action onUnauthorized)
    {
        Group = group;
        _directory = directory;
        _updates = updates;
        _onUnauthorized = onUnauthorized;
        Members = SectionState<IReadOnlyList<GroupMember>>.Loading;
    }

    public DirectoryGroup Group { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemberList))]
    public partial SectionState<IReadOnlyList<GroupMember>> Members { get; private set; }

    public IReadOnlyList<GroupMember> MemberList => Members.Kind == SectionKind.Loaded ? Members.Value! : [];

    /// <summary>The result of the last removal, shown above the list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    [ObservableProperty]
    public partial bool NoticeIsError { get; private set; }

    public bool HasNotice => Notice is not null;

    /// <summary>The member being removed, if any.</summary>
    [ObservableProperty]
    public partial string? Removing { get; private set; }

    public string Footer => Group.IsDynamic
        ? "Direct members only, first 200. This is a dynamic group, so members come from its rule and can't be removed here."
        : "Direct members only, first 200. To remove a member, right-click it or press the context menu key.";

    /// <summary>Dynamic groups take members from their rule; unknown object types aren't offered.</summary>
    public bool CanRemove(GroupMember member) => !Group.IsDynamic && member.Kind is not GroupMemberKind.Other;

    public static string RemovalTitle(GroupMember member, DirectoryGroup group) => $"Remove {member.DisplayName} from {group.DisplayName}?";

    [RelayCommand]
    public async Task LoadAsync()
    {
        Members = SectionState<IReadOnlyList<GroupMember>>.Loading;
        try
        {
            var found = await _directory.MembersAsync(Group.Id);
            Members = found.Count == 0
                ? SectionState<IReadOnlyList<GroupMember>>.NotFound
                : SectionState<IReadOnlyList<GroupMember>>.Loaded(
                    found.OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList());
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.Groups) is not { } message) return;
            Members = SectionState<IReadOnlyList<GroupMember>>.Failed(message);
        }
    }

    /// <summary>Removes a direct member. Returns true on success.</summary>
    public async Task<bool> RemoveAsync(GroupMember member)
    {
        Removing = member.Id;
        try
        {
            await _updates.RemoveMemberAsync(member.Id, Group.Id);
            if (Members.Kind == SectionKind.Loaded)
            {
                var remaining = Members.Value!.Where(other => other.Id != member.Id).ToList();
                Members = remaining.Count == 0
                    ? SectionState<IReadOnlyList<GroupMember>>.NotFound
                    : SectionState<IReadOnlyList<GroupMember>>.Loaded(remaining);
            }
            ShowNotice($"Removed {member.DisplayName}.", isError: false);
            return true;
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.GroupMembership) is { } message)
            {
                ShowNotice(message, isError: true);
            }
            return false;
        }
        finally
        {
            Removing = null;
        }
    }

    private void ShowNotice(string text, bool isError)
    {
        NoticeIsError = isError;
        Notice = text;
    }
}
