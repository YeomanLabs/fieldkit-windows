using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>One kind's section on an assignments or group page: what applies, or why it can't be shown.</summary>
public sealed partial class AssignmentKindSection : ObservableObject
{
    public AssignmentKindSection(AssignmentKind kind, string notFoundText)
    {
        Kind = kind;
        NotFoundText = notFoundText;
        State = SectionState<IReadOnlyList<ResolvedAssignment>>.Loading;
    }

    public AssignmentKind Kind { get; }

    public string Title => Kind.Title();

    /// <summary>"None targeted." on a device or person; "Nothing targets this group." on a group.</summary>
    public string NotFoundText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Items))]
    public partial SectionState<IReadOnlyList<ResolvedAssignment>> State { get; set; }

    public IReadOnlyList<ResolvedAssignment> Items => State.Kind == SectionKind.Loaded ? State.Value! : [];

    /// <summary>The page's refresh, which refetches the catalog (as iOS's retry does).</summary>
    public System.Windows.Input.ICommand? RetryCommand { get; internal set; }

    /// <summary>The kind's raw catalog state, mapped as iOS does before resolving.</summary>
    internal static SectionState<IReadOnlyList<ResolvedAssignment>>? Unresolved(SectionState<IReadOnlyList<AssignedItem>> catalog) =>
        catalog.Kind switch
        {
            SectionKind.Loading => SectionState<IReadOnlyList<ResolvedAssignment>>.Loading,
            SectionKind.Failed => SectionState<IReadOnlyList<ResolvedAssignment>>.Failed(catalog.Message ?? ""),
            SectionKind.NotFound or SectionKind.Skipped => SectionState<IReadOnlyList<ResolvedAssignment>>.NotFound,
            _ => null,
        };

    internal static SectionState<IReadOnlyList<ResolvedAssignment>> FromResolved(IReadOnlyList<ResolvedAssignment> resolved) =>
        resolved.Count == 0
            ? SectionState<IReadOnlyList<ResolvedAssignment>>.NotFound
            : SectionState<IReadOnlyList<ResolvedAssignment>>.Loaded(resolved);
}
