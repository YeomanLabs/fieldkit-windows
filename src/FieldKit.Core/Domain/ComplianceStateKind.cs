namespace FieldKit.Core.Domain;

/// <summary>The cases of <see cref="ComplianceState"/>.</summary>
public enum ComplianceStateKind
{
    Unknown,
    Compliant,
    Noncompliant,
    InGracePeriod,
    Conflict,
    Error,
    ConfigManager,
    Other,
}
