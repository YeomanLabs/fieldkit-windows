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

    /// <summary>Which items may be missing because Graph wouldn't give their assignments (<see cref="UnreadNote"/>); empty for none.</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = "";

    /// <summary>The most items <see cref="UnreadNote"/> names before saying how many more.</summary>
    public const int MaxNamesInNote = 5;

    /// <summary>
    /// "Couldn't read the assignments of Old VPN fix, so it may be missing here." The kind loaded, but
    /// these items' assignments are unknown, so whether they apply can't be said either way.
    /// </summary>
    internal static string UnreadNote(IReadOnlyList<AssignedItem> catalog)
    {
        var unread = catalog.Where(item => item.AssignmentsUnread).Select(item => item.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        if (unread.Count == 0) return "";
        var names = unread.Count <= MaxNamesInNote
            ? string.Join(", ", unread)
            : $"{string.Join(", ", unread.Take(MaxNamesInNote))} and {unread.Count - MaxNamesInNote} more";
        return $"Graph wouldn't give the assignments of {names}, so {(unread.Count == 1 ? "it" : "they")} may be missing here.";
    }

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
