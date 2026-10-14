namespace FieldKit.Core.Domain;

public sealed record DirectoryGroup
{
    /// <summary>Always set by Graph; defaults only so XAML can create the type.</summary>
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<string> GroupTypes { get; init; } = [];

    public string? MembershipRule { get; init; }

    public bool SecurityEnabled { get; init; }

    public bool IsDynamic => HasType("DynamicMembership");

    public string KindTitle
    {
        get
        {
            var type = HasType("Unified") ? "Microsoft 365" : (SecurityEnabled ? "Security" : "Distribution");
            return IsDynamic ? $"{type} · Dynamic" : $"{type} · Assigned";
        }
    }

    private bool HasType(string groupType) =>
        GroupTypes.Any(type => string.Equals(type, groupType, StringComparison.OrdinalIgnoreCase));
}
