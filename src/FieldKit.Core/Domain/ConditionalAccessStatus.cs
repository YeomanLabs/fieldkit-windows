namespace FieldKit.Core.Domain;

/// <summary>A sign-in's overall Conditional Access result (Graph's <c>conditionalAccessStatus</c>).</summary>
public enum ConditionalAccessStatus
{
    Unknown,
    Success,
    Failure,
    NotApplied,
}
