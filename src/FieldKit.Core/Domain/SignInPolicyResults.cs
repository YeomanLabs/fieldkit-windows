namespace FieldKit.Core.Domain;

public static class SignInPolicyResults
{
    public static SignInPolicyResult FromRaw(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "success" => SignInPolicyResult.Success,
            "failure" => SignInPolicyResult.Failure,
            "notapplied" => SignInPolicyResult.NotApplied,
            "notenabled" => SignInPolicyResult.NotEnabled,
            "reportonlysuccess" => SignInPolicyResult.ReportOnlySuccess,
            "reportonlyfailure" => SignInPolicyResult.ReportOnlyFailure,
            "reportonlynotapplied" => SignInPolicyResult.ReportOnlyNotApplied,
            "reportonlyinterrupted" => SignInPolicyResult.ReportOnlyInterrupted,
            _ => SignInPolicyResult.Unknown,
        };

    public static string DisplayName(this SignInPolicyResult result) =>
        result switch
        {
            SignInPolicyResult.Success => "Passed",
            SignInPolicyResult.Failure => "Blocked",
            SignInPolicyResult.NotApplied => "Didn't apply",
            SignInPolicyResult.NotEnabled => "Off",
            SignInPolicyResult.ReportOnlySuccess => "Report-only: would pass",
            SignInPolicyResult.ReportOnlyFailure => "Report-only: would block",
            SignInPolicyResult.ReportOnlyNotApplied => "Report-only: wouldn't apply",
            SignInPolicyResult.ReportOnlyInterrupted => "Report-only: would prompt",
            _ => "Unknown",
        };

    /// <summary>Policies that took part: passed, blocked, or would have in report-only mode.</summary>
    public static bool Applied(this SignInPolicyResult result) =>
        result is SignInPolicyResult.Success or SignInPolicyResult.Failure
            or SignInPolicyResult.ReportOnlySuccess or SignInPolicyResult.ReportOnlyFailure or SignInPolicyResult.ReportOnlyInterrupted;
}
