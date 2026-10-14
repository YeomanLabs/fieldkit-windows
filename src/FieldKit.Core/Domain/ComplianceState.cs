namespace FieldKit.Core.Domain;

/// <summary>An Intune managed device's overall compliance.</summary>
public readonly record struct ComplianceState
{
    private readonly string? _value;

    private ComplianceState(ComplianceStateKind kind, string? value = null)
    {
        Kind = kind;
        _value = value;
    }

    public ComplianceStateKind Kind { get; }

    /// <summary>Graph's value, for states FieldKit doesn't know (<see cref="ComplianceStateKind.Other"/>).</summary>
    public string Value => _value ?? "";

    public static ComplianceState Compliant => new(ComplianceStateKind.Compliant);

    public static ComplianceState Noncompliant => new(ComplianceStateKind.Noncompliant);

    public static ComplianceState InGracePeriod => new(ComplianceStateKind.InGracePeriod);

    public static ComplianceState Conflict => new(ComplianceStateKind.Conflict);

    public static ComplianceState Error => new(ComplianceStateKind.Error);

    public static ComplianceState ConfigManager => new(ComplianceStateKind.ConfigManager);

    public static ComplianceState Unknown => new(ComplianceStateKind.Unknown);

    public static ComplianceState Other(string value) => new(ComplianceStateKind.Other, value);

    public static ComplianceState FromRaw(string? rawValue) =>
        rawValue?.ToLowerInvariant() switch
        {
            "compliant" => Compliant,
            "noncompliant" => Noncompliant,
            "ingraceperiod" => InGracePeriod,
            "conflict" => Conflict,
            "error" => Error,
            "configmanager" => ConfigManager,
            "unknown" or null or "" => Unknown,
            _ => Other(rawValue),
        };

    public string DisplayName =>
        Kind switch
        {
            ComplianceStateKind.Compliant => "Compliant",
            ComplianceStateKind.Noncompliant => "Noncompliant",
            ComplianceStateKind.InGracePeriod => "In grace period",
            ComplianceStateKind.Conflict => "Conflict",
            ComplianceStateKind.Error => "Error",
            ComplianceStateKind.ConfigManager => "Managed by ConfigMgr",
            ComplianceStateKind.Other => Value,
            _ => "Unknown",
        };
}
