using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>An <c>appliedConditionalAccessPolicy</c> in a sign-in.</summary>
internal sealed class AppliedConditionalAccessPolicyDto
{
    public string? Id { get; init; }

    public string? DisplayName { get; init; }

    public List<string>? EnforcedGrantControls { get; init; }

    public List<string>? EnforcedSessionControls { get; init; }

    public string? Result { get; init; }

    public SignInPolicy ToDomain() =>
        new(Id ?? "", DisplayName ?? "Unnamed policy", SignInPolicyResults.FromRaw(Result), EnforcedGrantControls ?? [], EnforcedSessionControls ?? []);
}
