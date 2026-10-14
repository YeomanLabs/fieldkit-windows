using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>The tenant's remediations, to pick one to run on a Windows device now (ports <c>RemediationPickerModel</c>).</summary>
public sealed partial class RemediationPickerViewModel : ObservableObject
{
    private readonly AssignmentCatalog _catalog;
    private readonly Action _onUnauthorized;

    public RemediationPickerViewModel(AssignmentCatalog catalog, Action onUnauthorized)
    {
        _catalog = catalog;
        _onUnauthorized = onUnauthorized;
        Scripts = SectionState<IReadOnlyList<AssignedItem>>.Loading;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Filtered))]
    public partial SectionState<IReadOnlyList<AssignedItem>> Scripts { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Filtered))]
    public partial string Query { get; set; } = "";

    /// <summary>Scripts whose name contains the query, A–Z.</summary>
    public IReadOnlyList<AssignedItem> Filtered
    {
        get
        {
            if (Scripts.Kind != SectionKind.Loaded) return [];
            var term = Query.Trim();
            return term.Length == 0
                ? Scripts.Value!
                : Scripts.Value!.Where(script => script.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }
    }

    public async Task LoadAsync(bool refresh = false)
    {
        if (await CatalogLoader.LoadAsync(AssignmentKind.Remediation, _catalog, refresh, _onUnauthorized) is not { } state) return;
        if (state.Kind == SectionKind.Loaded)
        {
            var sorted = state.Value!.OrderBy(script => script.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            Scripts = sorted.Count == 0
                ? SectionState<IReadOnlyList<AssignedItem>>.NotFound
                : SectionState<IReadOnlyList<AssignedItem>>.Loaded(sorted);
        }
        else
        {
            Scripts = state;
        }
    }
}
