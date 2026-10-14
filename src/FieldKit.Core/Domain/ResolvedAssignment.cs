namespace FieldKit.Core.Domain;

/// <summary>An item that applies (or is excluded) for a device or person, and why.</summary>
/// <param name="IncludedVia">"All devices", "All users", or group names.</param>
public sealed record ResolvedAssignment(
    AssignedItem Item,
    IReadOnlyList<string> Intents,
    IReadOnlyList<string> IncludedVia,
    IReadOnlyList<string> ExcludedVia)
{
    public string Id => Item.Id;

    public bool IsExcluded => ExcludedVia.Count > 0;
}
