namespace FieldKit.Core.Domain;

/// <summary>The cases of <see cref="PolicyComplianceState"/>.</summary>
public enum PolicyComplianceStateKind
{
    Unknown,
    Compliant,
    Noncompliant,
    Error,
    Conflict,
    InGracePeriod,
    NotApplicable,
    NotAssigned,
    Remediated,
    Other,
}
