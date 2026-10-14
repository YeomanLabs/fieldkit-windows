using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// One Required group on the install options view (ports <c>AppInstallOptionsView.optionRow</c>):
/// what joining it brings in, and add or remove buttons for the device and its user.
/// </summary>
public sealed partial class InstallOptionRow : ObservableObject
{
    private readonly InstallAppViewModel _owner;

    internal InstallOptionRow(InstallOption option, InstallAppViewModel owner)
    {
        Option = option;
        _owner = owner;
    }

    public InstallOption Option { get; }

    public DirectoryGroup Group => Option.Group;

    public bool IsDynamic => Group.IsDynamic;

    public bool IsAssigned => !Group.IsDynamic;

    public bool HasAlsoTargets => IsAssigned && Option.AlsoTargets.Count > 0;

    public string AlsoTargetsText => $"Also brings in: {InstallAppViewModel.Summary(Option.AlsoTargets)}";

    // Device

    public bool CanAddDevice => IsAssigned && !_owner.IsMember(InstallMember.Device, Group) && _owner.EntraObjectId is not null;

    public bool ShowsAddDevice => IsAssigned && !_owner.IsMember(InstallMember.Device, Group);

    public bool ShowsRemoveDevice => IsAssigned && _owner.IsDirectMember(InstallMember.Device, Group);

    public bool DeviceViaNested => IsAssigned && _owner.IsMember(InstallMember.Device, Group) && !_owner.IsDirectMember(InstallMember.Device, Group);

    // User

    public bool ShowsAddUser => IsAssigned && _owner.HasUser && !_owner.IsMember(InstallMember.User, Group);

    public bool ShowsRemoveUser => IsAssigned && _owner.HasUser && _owner.IsDirectMember(InstallMember.User, Group);

    public bool UserViaNested => IsAssigned && _owner.HasUser && _owner.IsMember(InstallMember.User, Group) && !_owner.IsDirectMember(InstallMember.User, Group);

    public string AddUserText => $"Add {_owner.UserName}";

    /// <summary>Called when memberships change.</summary>
    internal void Refresh() => OnPropertyChanged(string.Empty);
}
