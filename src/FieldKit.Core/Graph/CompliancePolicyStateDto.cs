using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary><c>GET /deviceManagement/managedDevices/{id}/deviceCompliancePolicyStates</c> item.</summary>
internal sealed class CompliancePolicyStateDto
{
    internal sealed class SettingStateDto
    {
        public string? Setting { get; init; }

        public string? SettingName { get; init; }

        public string? State { get; init; }

        public string? CurrentValue { get; init; }

        public string? ErrorDescription { get; init; }
    }

    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? State { get; init; }

    public IReadOnlyList<SettingStateDto>? SettingStates { get; init; }

    public CompliancePolicyState ToDomain() =>
        new(
            Id,
            DisplayName ?? "Unnamed policy",
            PolicyComplianceState.FromRaw(State),
            (SettingStates ?? []).Select(setting => new CompliancePolicySetting(
                CompliancePolicySetting.DisplayName(setting.SettingName, setting.Setting),
                PolicyComplianceState.FromRaw(setting.State),
                setting.CurrentValue.NullIfEmpty(),
                setting.ErrorDescription.NullIfEmpty())).ToList());
}
