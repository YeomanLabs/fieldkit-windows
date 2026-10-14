namespace FieldKit.Core.Domain;

public static class PolicyDeploymentStates
{
    /// <summary>
    /// The configuration report's <c>PolicyStatus</c>, which follows Graph's <c>complianceStatus</c>
    /// numbering (2 = succeeded, confirmed on a real tenant).
    /// </summary>
    public static PolicyDeploymentState FromReport(int? status) =>
        status switch
        {
            0 => PolicyDeploymentState.NotReported,
            1 => PolicyDeploymentState.NotApplicable,
            2 or 3 => PolicyDeploymentState.Succeeded,
            4 or 5 => PolicyDeploymentState.Error,
            6 => PolicyDeploymentState.Conflict,
            7 => PolicyDeploymentState.NotAssigned,
            _ => PolicyDeploymentState.Unknown,
        };

    public static string DisplayName(this PolicyDeploymentState state) =>
        state switch
        {
            PolicyDeploymentState.Error => "Error",
            PolicyDeploymentState.Conflict => "Conflict",
            PolicyDeploymentState.NotReported => "Not reported yet",
            PolicyDeploymentState.Succeeded => "Succeeded",
            PolicyDeploymentState.NotApplicable => "Not applicable",
            PolicyDeploymentState.NotAssigned => "Not assigned",
            _ => "Unknown",
        };
}
