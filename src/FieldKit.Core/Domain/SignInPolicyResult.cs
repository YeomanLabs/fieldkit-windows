namespace FieldKit.Core.Domain;

/// <summary>Graph's <c>appliedConditionalAccessPolicyResult</c>.</summary>
public enum SignInPolicyResult
{
    Unknown,
    Success,
    Failure,
    NotApplied,
    NotEnabled,
    ReportOnlySuccess,
    ReportOnlyFailure,
    ReportOnlyNotApplied,
    ReportOnlyInterrupted,
}
