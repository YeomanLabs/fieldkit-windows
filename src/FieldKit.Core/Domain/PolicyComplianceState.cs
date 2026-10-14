namespace FieldKit.Core.Domain;

/// <summary>State of one compliance policy or setting on a device.</summary>
public readonly record struct PolicyComplianceState
{
    private readonly string? _value;

    private PolicyComplianceState(PolicyComplianceStateKind kind, string? value = null)
    {
        Kind = kind;
        _value = value;
    }

    public PolicyComplianceStateKind Kind { get; }

    /// <summary>Graph's value, for states FieldKit doesn't know (<see cref="PolicyComplianceStateKind.Other"/>).</summary>
    public string Value => _value ?? "";

    public static PolicyComplianceState Compliant => new(PolicyComplianceStateKind.Compliant);

    public static PolicyComplianceState Noncompliant => new(PolicyComplianceStateKind.Noncompliant);

    public static PolicyComplianceState Error => new(PolicyComplianceStateKind.Error);

    public static PolicyComplianceState Conflict => new(PolicyComplianceStateKind.Conflict);

    public static PolicyComplianceState InGracePeriod => new(PolicyComplianceStateKind.InGracePeriod);

    public static PolicyComplianceState NotApplicable => new(PolicyComplianceStateKind.NotApplicable);

    public static PolicyComplianceState NotAssigned => new(PolicyComplianceStateKind.NotAssigned);

    public static PolicyComplianceState Remediated => new(PolicyComplianceStateKind.Remediated);

    public static PolicyComplianceState Unknown => new(PolicyComplianceStateKind.Unknown);

    public static PolicyComplianceState Other(string value) => new(PolicyComplianceStateKind.Other, value);

    public static PolicyComplianceState FromRaw(string? rawValue) =>
        rawValue?.ToLowerInvariant() switch
        {
            "compliant" => Compliant,
            "noncompliant" => Noncompliant,
            "error" => Error,
            "conflict" => Conflict,
            "ingraceperiod" => InGracePeriod,
            "notapplicable" => NotApplicable,
            "notassigned" => NotAssigned,
            "remediated" => Remediated,
            "unknown" or null or "" => Unknown,
            _ => Other(rawValue),
        };

    public string DisplayName =>
        Kind switch
        {
            PolicyComplianceStateKind.Compliant => "Compliant",
            PolicyComplianceStateKind.Noncompliant => "Not compliant",
            PolicyComplianceStateKind.Error => "Error",
            PolicyComplianceStateKind.Conflict => "Conflict",
            PolicyComplianceStateKind.InGracePeriod => "In grace period",
            PolicyComplianceStateKind.NotApplicable => "Not applicable",
            PolicyComplianceStateKind.NotAssigned => "Not assigned",
            PolicyComplianceStateKind.Remediated => "Remediated",
            PolicyComplianceStateKind.Other => Value,
            _ => "Unknown",
        };

    /// <summary>States an engineer needs to act on.</summary>
    public bool IsProblem =>
        Kind is PolicyComplianceStateKind.Noncompliant or PolicyComplianceStateKind.Error or PolicyComplianceStateKind.Conflict;

    /// <summary>Policies that don't apply to the device are hidden from the device page.</summary>
    public bool IsRelevant =>
        Kind is not (PolicyComplianceStateKind.NotApplicable or PolicyComplianceStateKind.NotAssigned);
}
