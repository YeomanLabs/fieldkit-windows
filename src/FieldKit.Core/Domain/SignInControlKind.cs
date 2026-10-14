namespace FieldKit.Core.Domain;

/// <summary>What a Conditional Access control asks for.</summary>
public enum SignInControlKind
{
    Other,
    Block,
    Mfa,
    AuthenticationStrength,
    CompliantDevice,
    HybridJoinedDevice,
    ApprovedApp,
    AppProtection,
    PasswordChange,
    TermsOfUse,
}
