using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary><c>GET /auditLogs/signIns</c> item (v1.0).</summary>
internal sealed class SignInDto
{
    public required string Id { get; init; }

    public DateTimeOffset? CreatedDateTime { get; init; }

    public string? UserDisplayName { get; init; }

    public string? UserPrincipalName { get; init; }

    public string? AppDisplayName { get; init; }

    public string? ResourceDisplayName { get; init; }

    public string? ClientAppUsed { get; init; }

    public string? IpAddress { get; init; }

    public string? CorrelationId { get; init; }

    public string? ConditionalAccessStatus { get; init; }

    public bool? IsInteractive { get; init; }

    public SignInStatusDto? Status { get; init; }

    public SignInDeviceDetailDto? DeviceDetail { get; init; }

    public SignInLocationDto? Location { get; init; }

    /// <summary>Left out entirely (null) when the caller can't read Conditional Access data.</summary>
    public List<AppliedConditionalAccessPolicyDto>? AppliedConditionalAccessPolicies { get; init; }

    public SignIn ToDomain() =>
        new()
        {
            Id = Id,
            CreatedAt = CreatedDateTime ?? DateTimeOffset.MinValue,
            UserDisplayName = UserDisplayName ?? "",
            UserPrincipalName = UserPrincipalName ?? "",
            AppName = AppDisplayName ?? "",
            ResourceName = ResourceDisplayName ?? "",
            ClientApp = ClientAppUsed ?? "",
            IpAddress = IpAddress ?? "",
            Location = Location?.ToText() ?? "",
            IsInteractive = IsInteractive ?? true,
            ErrorCode = Status?.ErrorCode ?? 0,
            FailureReason = Status?.FailureReason ?? "",
            AdditionalDetails = Status?.AdditionalDetails ?? "",
            ConditionalAccess = ConditionalAccessStatuses.FromRaw(ConditionalAccessStatus),
            Policies = AppliedConditionalAccessPolicies?.Select(policy => policy.ToDomain()).ToList(),
            Device = DeviceDetail?.ToDomain() ?? new SignInDevice(),
            CorrelationId = CorrelationId ?? "",
        };
}
