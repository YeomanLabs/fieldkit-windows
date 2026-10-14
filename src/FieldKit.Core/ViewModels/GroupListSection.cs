using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>A list of groups on the assignments page: the device's own, or a person's.</summary>
public sealed partial class GroupListSection : ObservableObject
{
    private readonly string _title;

    public GroupListSection(string title)
    {
        _title = title;
        State = SectionState<IReadOnlyList<DirectoryGroup>>.Loading;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Groups), nameof(Header))]
    public partial SectionState<IReadOnlyList<DirectoryGroup>> State { get; set; }

    public IReadOnlyList<DirectoryGroup> Groups => State.Kind == SectionKind.Loaded ? State.Value! : [];

    /// <summary>"Device groups (12)" once loaded.</summary>
    public string Header => State.Kind == SectionKind.Loaded ? $"{_title} ({Groups.Count})" : _title;

    public string NotFoundText => "Not in any groups.";
}
