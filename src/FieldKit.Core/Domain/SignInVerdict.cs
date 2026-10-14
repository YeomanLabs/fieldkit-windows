namespace FieldKit.Core.Domain;

/// <summary>How a sign-in ended, in helpdesk terms.</summary>
public enum SignInVerdict
{
    /// <summary>Signed in.</summary>
    Success,

    /// <summary>Stopped for a prompt (MFA, "Stay signed in?", a password change); often followed by a success.</summary>
    Interrupted,

    /// <summary>Refused for a reason other than Conditional Access (wrong password, disabled account ...).</summary>
    Failed,

    /// <summary>Refused by a Conditional Access policy.</summary>
    Blocked,
}
