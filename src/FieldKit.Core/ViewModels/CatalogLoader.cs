using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>Loads catalog kinds into section states for a feature view model (ports <c>CatalogLoader</c>).</summary>
internal static class CatalogLoader
{
    /// <returns>Null when the section should be left alone (cancelled, or the session expired).</returns>
    public static async Task<SectionState<IReadOnlyList<AssignedItem>>?> LoadAsync(
        AssignmentKind kind,
        AssignmentCatalog catalog,
        bool refresh,
        Action onUnauthorized,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return SectionState<IReadOnlyList<AssignedItem>>.Loaded(await catalog.ItemsAsync(kind, refresh, cancellationToken));
        }
        catch (Exception error)
        {
            return FeatureErrors.Message(error, onUnauthorized, kind.Feature()) is { } message
                ? SectionState<IReadOnlyList<AssignedItem>>.Failed(message)
                : null;
        }
    }
}
