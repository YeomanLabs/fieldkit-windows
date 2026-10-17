using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>One group and what targets it, by kind (ports <c>GroupModel</c>).</summary>
public sealed partial class GroupViewModel : ObservableObject
{
    private readonly AssignmentCatalog _catalog;
    private readonly Action _onUnauthorized;
    private CancellationTokenSource _cancellation = new();

    public GroupViewModel(DirectoryGroup group, AssignmentCatalog catalog, Action onUnauthorized)
    {
        Group = group;
        _catalog = catalog;
        _onUnauthorized = onUnauthorized;
        Sections = Enum.GetValues<AssignmentKind>()
            .Select(kind => new AssignmentKindSection(kind, "Nothing targets this group.") { RetryCommand = RefreshCommand })
            .ToList();
    }

    public DirectoryGroup Group { get; }

    public string Name => Group.DisplayName;

    public string KindTitle => Group.KindTitle;

    public bool HasDescription => Group.Description.Length > 0;

    public string MembershipRule => Group.MembershipRule ?? "";

    public bool HasMembershipRule => Group.MembershipRule is not null;

    public IReadOnlyList<AssignmentKindSection> Sections { get; }

    [RelayCommand]
    public Task LoadAsync() => LoadAllAsync(refresh: false);

    [RelayCommand]
    public Task RefreshAsync() => LoadAllAsync(refresh: true);

    public void Cancel()
    {
        _cancellation.Cancel();
        _cancellation = new CancellationTokenSource();
    }

    private Task LoadAllAsync(bool refresh)
    {
        var cancellationToken = _cancellation.Token;
        return Task.WhenAll(Sections.Select(section => LoadSectionAsync(section, refresh, cancellationToken)));
    }

    private async Task LoadSectionAsync(AssignmentKindSection section, bool refresh, CancellationToken cancellationToken)
    {
        section.State = SectionState<IReadOnlyList<ResolvedAssignment>>.Loading;
        section.Note = "";
        if (await CatalogLoader.LoadAsync(section.Kind, _catalog, refresh, _onUnauthorized, cancellationToken) is not { } catalog) return;
        section.Note = catalog.Kind == SectionKind.Loaded ? AssignmentKindSection.UnreadNote(catalog.Value!) : "";
        section.State = AssignmentKindSection.Unresolved(catalog)
            ?? AssignmentKindSection.FromResolved(AssignmentResolver.Targeting(Group.Id, Group.DisplayName, catalog.Value!));
    }
}
