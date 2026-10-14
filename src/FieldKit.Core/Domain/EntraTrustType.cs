namespace FieldKit.Core.Domain;

public readonly record struct EntraTrustType
{
    private readonly string? _value;

    private EntraTrustType(EntraTrustTypeKind kind, string? value = null)
    {
        Kind = kind;
        _value = value;
    }

    public EntraTrustTypeKind Kind { get; }

    /// <summary>Graph's value, for trust types FieldKit doesn't know (<see cref="EntraTrustTypeKind.Other"/>).</summary>
    public string Value => _value ?? "";

    public static EntraTrustType EntraJoined => new(EntraTrustTypeKind.EntraJoined);

    public static EntraTrustType HybridJoined => new(EntraTrustTypeKind.HybridJoined);

    public static EntraTrustType Registered => new(EntraTrustTypeKind.Registered);

    public static EntraTrustType None => new(EntraTrustTypeKind.None);

    public static EntraTrustType Other(string value) => new(EntraTrustTypeKind.Other, value);

    public static EntraTrustType FromRaw(string? rawValue) =>
        rawValue?.ToLowerInvariant() switch
        {
            "azuread" => EntraJoined,
            "serverad" => HybridJoined,
            "workplace" => Registered,
            null or "" => None,
            _ => Other(rawValue),
        };

    public string DisplayName =>
        Kind switch
        {
            EntraTrustTypeKind.EntraJoined => "Entra joined",
            EntraTrustTypeKind.HybridJoined => "Hybrid joined",
            EntraTrustTypeKind.Registered => "Registered",
            EntraTrustTypeKind.Other => Value,
            _ => "Not set",
        };
}
