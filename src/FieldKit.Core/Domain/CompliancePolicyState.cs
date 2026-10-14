namespace FieldKit.Core.Domain;

/// <summary>One compliance policy's result on a device, for "why is it noncompliant?".</summary>
public sealed record CompliancePolicyState(
    string Id,
    string DisplayName,
    PolicyComplianceState State,
    IReadOnlyList<CompliancePolicySetting> Settings)
{
    public IReadOnlyList<CompliancePolicySetting> FailingSettings =>
        Settings.Where(setting => setting.State.IsProblem).ToList();

    /// <summary>Intune says the policy failed but not which setting did.</summary>
    public bool FailedWithoutDetail => State.IsProblem && FailingSettings.Count == 0;

    /// <summary>Relevant policies, problems first, then by name.</summary>
    public static IReadOnlyList<CompliancePolicyState> SortedForDisplay(IEnumerable<CompliancePolicyState> policies) =>
        policies
            .Where(policy => policy.State.IsRelevant)
            .OrderByDescending(policy => policy.State.IsProblem)
            .ThenBy(policy => policy.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
