using System.Text.RegularExpressions;

namespace FieldKit.Core.Domain;

/// <summary>
/// "Why was I blocked?": turns a sign-in record into a plain verdict, the reason, and what to
/// check. Conditional Access failures are explained from the failing policies' controls and the
/// device Entra saw; other failures from the AADSTS code (descriptions checked against
/// Microsoft's error code reference on 2026-10-14), falling back to Entra's own failure reason.
/// </summary>
public static partial class SignInDecoder
{
    /// <summary>Codes that mean Conditional Access refused the sign-in.</summary>
    private static readonly HashSet<int> ConditionalAccessCodes = [53000, 53001, 53002, 53003, 530032];

    /// <summary>Client apps that can't do modern authentication; "Block legacy authentication" policies stop them.</summary>
    private static bool IsLegacyClient(string clientApp) =>
        clientApp.Length > 0
        && !clientApp.Equals("Browser", StringComparison.OrdinalIgnoreCase)
        && !clientApp.StartsWith("Mobile Apps and Desktop clients", StringComparison.OrdinalIgnoreCase);

    public static SignInDiagnosis Diagnose(SignIn signIn)
    {
        var failed = signIn.Policies?.Where(policy => policy.Failed).ToList() ?? [];
        if (signIn.ConditionalAccess == ConditionalAccessStatus.Failure || failed.Count > 0 || ConditionalAccessCodes.Contains(signIn.ErrorCode))
        {
            return Blocked(signIn, failed);
        }
        return signIn.ErrorCode == 0 ? Success(signIn) : ByCode(signIn);
    }

    private static SignInDiagnosis Success(SignIn signIn)
    {
        var check = new List<string>();
        string explanation;
        if (signIn.Policies is not { } policies)
        {
            explanation = "Signed in. FieldKit can't see which Conditional Access policies applied (that needs Policy.Read.ConditionalAccess and a role such as Global Reader or Security Reader).";
        }
        else
        {
            var passed = policies.Count(policy => policy.Result == SignInPolicyResult.Success);
            explanation = passed switch
            {
                0 => "Signed in. No Conditional Access policy applied.",
                1 => "Signed in. 1 Conditional Access policy applied and passed.",
                _ => $"Signed in. {passed} Conditional Access policies applied and passed.",
            };
            foreach (var wouldBlock in policies.Where(policy => policy.Result == SignInPolicyResult.ReportOnlyFailure))
            {
                check.Add($"Report-only policy “{wouldBlock.Name}” would block this sign-in once it's turned on.");
            }
        }
        return new SignInDiagnosis(SignInVerdict.Success, "Signed in", explanation, check);
    }

    private static SignInDiagnosis Blocked(SignIn signIn, List<SignInPolicy> failed)
    {
        var reasons = new List<string>();
        var check = new List<string>();
        var device = signIn.Device;
        var deviceName = device.DisplayName.Length > 0 ? device.DisplayName : "the device";

        if (signIn.Policies is null)
        {
            reasons.Add(ByConditionalAccessCode(signIn.ErrorCode));
            reasons.Add("FieldKit can't see which policy: that needs the Policy.Read.ConditionalAccess permission and a role such as Global Reader, Security Reader or Conditional Access Administrator.");
        }
        else if (failed.Count == 0)
        {
            reasons.Add(ByConditionalAccessCode(signIn.ErrorCode));
        }

        foreach (var policy in failed)
        {
            var kinds = policy.GrantControls.Select(SignInControls.Kind).ToHashSet();
            var needs = policy.GrantControls.Count == 0 ? "" : $" ({policy.ControlsText})";
            reasons.Add($"Policy “{policy.Name}” refused it{needs}.");

            if (kinds.Contains(SignInControlKind.Block))
            {
                if (IsLegacyClient(signIn.ClientApp))
                {
                    reasons.Add($"The sign-in came from {signIn.ClientApp}, a legacy client that can't do modern authentication, and the policy blocks it outright.");
                    check.Add("Move the user to Outlook or another app that uses modern authentication; legacy protocols stay blocked.");
                }
                else
                {
                    var from = signIn.Location.Length > 0 ? $" from {signIn.Location}" : "";
                    reasons.Add($"The policy blocks this sign-in outright{from}: something about it (location, app, platform or risk) matches the policy's conditions.");
                    check.Add($"In Entra, open Conditional Access → “{policy.Name}” and compare its conditions with this sign-in's app, platform and location.");
                }
            }
            if (kinds.Contains(SignInControlKind.CompliantDevice))
            {
                if (!device.HasIdentity)
                {
                    reasons.Add("The sign-in carried no device identity, so Entra couldn't check compliance. That happens in an InPrivate window, in a browser without Microsoft single sign-on, or on a device that isn't joined or registered.");
                    check.Add("Sign in with Edge, or Chrome with the Microsoft Single Sign On extension, in a normal window.");
                    check.Add("Check the device is Entra joined, hybrid joined or registered.");
                }
                else if (device.IsCompliant == false)
                {
                    reasons.Add($"Entra sees {deviceName} as not compliant.");
                    check.Add($"Open {deviceName} in FieldKit and look at its compliance policies.");
                    check.Add("Once fixed, Sync the device and give compliance a few minutes to re-evaluate.");
                }
                else if (device.IsCompliant == true)
                {
                    reasons.Add($"Entra saw {deviceName} as compliant, so another requirement in the policy wasn't met.");
                }
            }
            if (kinds.Contains(SignInControlKind.HybridJoinedDevice) && !device.TrustType.Contains("Hybrid", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(device.HasIdentity
                    ? $"{deviceName} isn't hybrid joined ({(device.TrustType.Length > 0 ? device.TrustType : "unknown join type")})."
                    : "The sign-in carried no device identity, so Entra couldn't tell it came from a hybrid joined device.");
                check.Add("Check the device's join type in Entra and that Entra Connect is syncing it.");
            }
            if (kinds.Contains(SignInControlKind.Mfa) || kinds.Contains(SignInControlKind.AuthenticationStrength))
            {
                reasons.Add("The user didn't complete the multifactor authentication the policy asks for.");
                check.Add("Check the user's registered authentication methods, and whether they're getting the prompt.");
            }
            if (kinds.Contains(SignInControlKind.ApprovedApp) || kinds.Contains(SignInControlKind.AppProtection))
            {
                reasons.Add($"The policy only allows approved apps or apps with an app protection policy; {(signIn.AppName.Length > 0 ? signIn.AppName : "this app")} doesn't qualify here.");
                check.Add("Use the Microsoft app (Outlook, Teams ...) covered by your app protection policies.");
            }
            if (kinds.Contains(SignInControlKind.PasswordChange))
            {
                reasons.Add("The policy requires a password change (usually because of user risk).");
                check.Add("Have the user change their password, then sign in again.");
            }
        }

        if (failed.Count == 0)
        {
            check.AddRange(signIn.ErrorCode switch
            {
                53000 => ["Open the device in FieldKit and look at its compliance policies."],
                53001 => ["Check the device's join type in Entra and that Entra Connect is syncing it."],
                53002 => ["Use the Microsoft app covered by your app protection policies."],
                _ => ["In Entra, open Conditional Access and check which policy targets this user and app."],
            });
        }
        check.Add($"Request ID {signIn.Id} finds this sign-in in Entra's sign-in logs.");
        return new SignInDiagnosis(SignInVerdict.Blocked, "Blocked by Conditional Access", string.Join(" ", reasons.Distinct()), check.Distinct().ToList());
    }

    private static string ByConditionalAccessCode(int code) =>
        code switch
        {
            53000 => "Conditional Access requires a compliant device, and this one isn't compliant.",
            53001 => "Conditional Access requires a hybrid joined device, and this one isn't.",
            53002 => "Conditional Access requires an approved app, and this one isn't.",
            530032 => "A tenant security policy (such as security defaults) blocked this request.",
            _ => "Conditional Access blocked this sign-in.",
        };

    private static string AppLabel(SignIn signIn) => signIn.AppName.Length > 0 ? signIn.AppName : "the app";

    [GeneratedRegex(@"\{[A-Za-z]+\}")]
    private static partial Regex Placeholder();

    private static SignInDiagnosis ByCode(SignIn signIn)
    {
        var request = $"Request ID {signIn.Id} finds this sign-in in Entra's sign-in logs.";
        (SignInVerdict Verdict, string Title, string Explanation, string[] Check) found = signIn.ErrorCode switch
        {
            50126 => (SignInVerdict.Failed, "Wrong username or password",
                "Entra couldn't validate the credentials. A few of these are normal typos.",
                ["If it keeps happening, check for a recent password change, caps lock, or sign-ins from places the user doesn't recognise."]),
            50053 => (SignInVerdict.Failed, "Account locked",
                "Too many attempts with the wrong password (smart lockout), or sign-ins from an IP address Microsoft flags as malicious.",
                ["Wait for the lockout to expire, and reset the password if the attempts weren't the user's."]),
            50057 => (SignInVerdict.Failed, "Account disabled",
                "The user account is disabled.",
                ["Re-enable it in Entra, or in Active Directory if the account syncs from there."]),
            50055 => (SignInVerdict.Failed, "Password expired",
                "The user's password has expired.",
                ["Have the user change it, or reset it for them."]),
            50133 or 50173 => (SignInVerdict.Interrupted, "Session ended by a password change",
                "The password was changed or reset, so the old session no longer counts.",
                ["Sign in again with the new password."]),
            50074 or 50076 => (SignInVerdict.Interrupted, "Multifactor authentication needed",
                "Entra asked for multifactor authentication. If a successful sign-in follows, this is normal.",
                ["If none follows, check the user's registered authentication methods."]),
            50072 or 50079 or 53004 => (SignInVerdict.Interrupted, "Needs to register for MFA",
                "The user has to set up multifactor authentication before signing in.",
                ["Have the user register at https://aka.ms/mysecurityinfo."]),
            50097 => (SignInVerdict.Failed, "Device authentication required",
                "The app needed the device to prove its identity, and it couldn't.",
                ["Check the device is joined or registered in Entra and has a valid sign-in token (dsregcmd /status on the PC)."]),
            50105 => (SignInVerdict.Failed, "Not assigned to the app",
                "The app requires assignment, and this user isn't assigned to it.",
                ["Assign the user, or a group they're in, under Enterprise applications → the app → Users and groups."]),
            50011 => (SignInVerdict.Failed, "Redirect URI not registered",
                $"{AppLabel(signIn)} tried to finish signing in at a redirect URI its app registration doesn't list. This is an app setup problem, not the user's.",
                [$"In Entra → App registrations → {AppLabel(signIn)} → Authentication, add the redirect URI the app uses (its setup instructions list it)."]),
            7000218 => (SignInVerdict.Failed, "App registration expects a secret",
                $"{AppLabel(signIn)} is a desktop or mobile app, but its registration treats it as a confidential client that needs a secret.",
                [$"In Entra → App registrations → {AppLabel(signIn)} → Authentication, turn on “Allow public client flows”."]),
            65001 or 90094 => (SignInVerdict.Failed, "Consent missing",
                $"Nobody has granted {AppLabel(signIn)} the permissions it asks for in this tenant.",
                [$"In Entra → App registrations → {AppLabel(signIn)} → API permissions, select Grant admin consent."]),
            700016 => (SignInVerdict.Failed, "App not found in this tenant",
                "The app's client ID isn't registered in this tenant, or the app is pointed at the wrong tenant.",
                ["Check the client ID and tenant ID the app is set up with."]),
            50034 or 50020 => (SignInVerdict.Failed, "Account not in this directory",
                "The account isn't in this tenant: a typo in the username, the wrong tenant, or an outside account that hasn't been invited as a guest.",
                ["Check the username, and invite the account as a guest if it belongs to another organization."]),
            50056 => (SignInVerdict.Failed, "Password not recognised",
                "Entra has no matching password for this account.",
                ["Have the user enter the password again; if the account syncs from AD, check password hash sync."]),
            50194 => (SignInVerdict.Failed, "App isn't multitenant",
                $"{AppLabel(signIn)} signed in through the shared “common” endpoint, but its registration only allows its own tenant.",
                ["Set the app up with the tenant ID, or make the registration multitenant."]),
            50158 => (SignInVerdict.Interrupted, "Extra verification required",
                "Another identity provider or security challenge had to be satisfied first.",
                []),
            50058 or 50140 or 50199 or 50125 or 81010 or 700082 => (SignInVerdict.Interrupted, "Interrupted",
                signIn.ErrorCode switch
                {
                    50140 => "The user was asked “Stay signed in?”. This is a normal part of signing in.",
                    50199 => "The user was asked to confirm the app. This is a normal part of signing in.",
                    50125 => "Sign-in paused for a password reset or registration.",
                    81010 => "Seamless single sign-on didn't work (the Kerberos ticket expired), so Entra fell back to asking.",
                    700082 => "The saved sign-in expired after a long time unused, so the user had to sign in again.",
                    _ => "Single sign-on had nothing to use, so the user was asked to sign in. This is normal.",
                },
                []),
            _ => (SignInVerdict.Failed, "Sign-in failed",
                // Entra sometimes returns its message template unfilled ("The {redirectTerm} '{replyAddress}' ...").
                signIn.FailureReason.Length > 0 && !Placeholder().IsMatch(signIn.FailureReason)
                    ? signIn.FailureReason
                    : $"Entra returned error AADSTS{signIn.ErrorCode}.",
                [$"Look up AADSTS{signIn.ErrorCode} in Microsoft's Entra error code reference."]),
        };
        return new SignInDiagnosis(found.Verdict, found.Title, found.Explanation, [.. found.Check, request]);
    }
}
