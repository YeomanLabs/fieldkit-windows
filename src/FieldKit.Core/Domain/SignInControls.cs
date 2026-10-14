using System.Text.RegularExpressions;

namespace FieldKit.Core.Domain;

/// <summary>
/// Reads Conditional Access grant and session control names from sign-in logs. Graph's names
/// vary ("Mfa", "RequireCompliantDevice", "Require multifactor authentication"), so they're
/// matched loosely.
/// </summary>
public static partial class SignInControls
{
    public static SignInControlKind Kind(string control)
    {
        var key = Squash().Replace(control.ToLowerInvariant(), "");
        if (key.Contains("block", StringComparison.Ordinal)) return SignInControlKind.Block;
        if (key.Contains("authenticationstrength", StringComparison.Ordinal) || key.Contains("authstrength", StringComparison.Ordinal)) return SignInControlKind.AuthenticationStrength;
        if (key.Contains("mfa", StringComparison.Ordinal) || key.Contains("multifactor", StringComparison.Ordinal)) return SignInControlKind.Mfa;
        if (key.Contains("domainjoined", StringComparison.Ordinal) || key.Contains("hybrid", StringComparison.Ordinal)) return SignInControlKind.HybridJoinedDevice;
        // "RequireCompliantDevice", and the portal's "Require device to be marked as compliant".
        if (key.Contains("compliant", StringComparison.Ordinal) && key.Contains("device", StringComparison.Ordinal)) return SignInControlKind.CompliantDevice;
        if (key.Contains("approved", StringComparison.Ordinal)) return SignInControlKind.ApprovedApp;
        if (key.Contains("compliantapp", StringComparison.Ordinal) || key.Contains("appprotection", StringComparison.Ordinal)) return SignInControlKind.AppProtection;
        if (key.Contains("passwordchange", StringComparison.Ordinal)) return SignInControlKind.PasswordChange;
        if (key.Contains("termsofuse", StringComparison.Ordinal)) return SignInControlKind.TermsOfUse;
        return SignInControlKind.Other;
    }

    /// <summary>The control as the Conditional Access blade words it.</summary>
    public static string Describe(string control) =>
        Kind(control) switch
        {
            SignInControlKind.Block => "Block access",
            SignInControlKind.Mfa => "Require multifactor authentication",
            SignInControlKind.AuthenticationStrength => "Require authentication strength",
            SignInControlKind.CompliantDevice => "Require compliant device",
            SignInControlKind.HybridJoinedDevice => "Require hybrid joined device",
            SignInControlKind.ApprovedApp => "Require approved client app",
            SignInControlKind.AppProtection => "Require app protection policy",
            SignInControlKind.PasswordChange => "Require password change",
            SignInControlKind.TermsOfUse => "Require terms of use",
            _ => Words().Replace(control, "$1 $2").Trim(),
        };

    [GeneratedRegex("[^a-z]")]
    private static partial Regex Squash();

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex Words();
}
