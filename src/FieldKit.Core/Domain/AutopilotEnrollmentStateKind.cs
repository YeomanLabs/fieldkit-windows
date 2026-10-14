namespace FieldKit.Core.Domain;

/// <summary>The cases of <see cref="AutopilotEnrollmentState"/>.</summary>
public enum AutopilotEnrollmentStateKind
{
    Unknown,
    Enrolled,
    NotContacted,
    PendingReset,
    Failed,
    Other,
}
