namespace FieldKit.Core.Domain;

/// <summary>
/// One interactive Entra sign-in (Graph <c>auditLogs/signIns</c>), for "why was I blocked?"
/// (Windows only; the iOS roadmap's phase 4, added 2026-10-14).
/// </summary>
public sealed record SignIn
{
    public string Id { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }

    public string UserDisplayName { get; init; } = "";

    public string UserPrincipalName { get; init; } = "";

    /// <summary>The app signed in to ("Microsoft Teams").</summary>
    public string AppName { get; init; } = "";

    /// <summary>What the app asked for a token to ("Office 365 Exchange Online").</summary>
    public string ResourceName { get; init; } = "";

    /// <summary>"Browser", "Mobile Apps and Desktop clients", "Exchange ActiveSync" ...</summary>
    public string ClientApp { get; init; } = "";

    public string IpAddress { get; init; } = "";

    /// <summary>"Redmond, Washington, US", or empty.</summary>
    public string Location { get; init; } = "";

    public bool IsInteractive { get; init; } = true;

    /// <summary>0 when the sign-in succeeded; otherwise the AADSTS number.</summary>
    public int ErrorCode { get; init; }

    /// <summary>Entra's own description of the failure.</summary>
    public string FailureReason { get; init; } = "";

    public string AdditionalDetails { get; init; } = "";

    public ConditionalAccessStatus ConditionalAccess { get; init; } = ConditionalAccessStatus.Unknown;

    /// <summary>
    /// The Conditional Access policies Entra evaluated, or null when Graph left them out because
    /// the app or the signed-in admin can't read Conditional Access data.
    /// </summary>
    public IReadOnlyList<SignInPolicy>? Policies { get; init; }

    public SignInDevice Device { get; init; } = new();

    /// <summary>For Microsoft support, alongside <see cref="Id"/> (the request ID).</summary>
    public string CorrelationId { get; init; } = "";
}
