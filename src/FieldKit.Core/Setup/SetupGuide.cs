using FieldKit.Core.Auth;

namespace FieldKit.Core.Setup;

/// <summary>
/// The Entra app registration steps for Windows, shared by the setup page and the text sent to
/// an admin. Ports the iOS guide; the platform step adds the Windows broker redirect URI.
/// </summary>
public sealed class SetupGuide
{
    public static readonly Uri AdminCenterUrl = new("https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade");

    public const string AppName = "FieldKit";

    /// <summary>Stands in for the client ID in the redirect URI until a valid one has been typed.</summary>
    public const string ClientIdPlaceholder = "<client ID>";

    public const string ExistingRegistrationNote =
        "Already registered FieldKit for iPhone? Use the same registration: just add this platform and enter the same IDs.";

    public const string NoSecretNote =
        "No client secret or certificate. FieldKit is a public client: each engineer signs in "
        + "as themselves and Windows keeps their tokens in its account broker. A secret in a desktop "
        + "app can be extracted, so don't create one for this registration.";

    public const string ConditionalAccessNote =
        "Conditional Access: FieldKit signs in through Windows, which passes the PC's device state, "
        + "so a policy that requires a compliant device works on Intune-managed PCs. On PCs that "
        + "aren't managed, that policy blocks sign-in, which is expected.";

    /// <param name="clientId">What's been typed in the client ID box so far, if anything.</param>
    public SetupGuide(string? clientId = null)
    {
        ClientId = clientId is null ? null : AuthConfig.NormalizedClientId(clientId);
    }

    /// <summary>The typed client ID once it's a valid GUID; null before then.</summary>
    public string? ClientId { get; }

    /// <summary>The broker (WAM) redirect URI for this registration.</summary>
    public string RedirectUri => $"ms-appx-web://microsoft.aad.brokerplugin/{ClientId ?? ClientIdPlaceholder}";

    /// <summary>Permission names as the portal lists them.</summary>
    public static IReadOnlyList<SetupItem> ScopeItems =>
        GraphPermission.All.Select(permission => new SetupItem(permission.Purpose, permission.Name)).ToList();

    public IReadOnlyList<SetupStep> Steps =>
    [
        new SetupStep(1, "Register the app",
            "In the Entra admin center go to App registrations → New registration. Use this name, choose Single tenant, and leave the redirect URI empty for now.",
            [new SetupItem("Name", AppName)]),
        new SetupStep(2, "Add the Windows platform",
            ClientId is null
                ? "In the new registration open Authentication → Add a platform → Mobile and desktop applications. Under custom redirect URIs, add this one with your Application (client) ID in place of the placeholder (enter the ID above and FieldKit fills it in), then select Configure."
                : "In the new registration open Authentication → Add a platform → Mobile and desktop applications. Under custom redirect URIs, add this one, then select Configure.",
            [new SetupItem("Redirect URI", RedirectUri)]),
        new SetupStep(3, "Add Graph permissions",
            "API permissions → Add a permission → Microsoft Graph → Delegated permissions. Add these, then select Grant admin consent. Directory.AccessAsUser.All is optional: add it only to edit device extension attributes, because it lets FieldKit do anything your own Entra role allows. Only Autopilot pre-staging, device actions, app installs (group membership) and extension attributes can change anything; everything else is read-only.",
            ScopeItems),
        new SetupStep(4, "Limit who can sign in",
            $"Enterprise applications → {AppName} → Properties: set Assignment required to Yes. Then under Users and groups, assign your IT group.",
            []),
        new SetupStep(5, "Give engineers the right roles",
            "Permissions only let FieldKit ask; each engineer's own roles decide what they can do. For lookups: an Intune role that can read managed devices (for example Read Only Operator), with scope tags covering their devices. For sync, restart, retire and Autopilot changes: an Intune role with those rights (for example Help Desk Operator, or Policy and Profile Manager for Autopilot). For LAPS passwords and BitLocker keys: an Entra role such as Cloud Device Administrator. Anything missing shows as \"Access denied\" naming what's needed.",
            []),
        new SetupStep(6, "Copy the IDs",
            "On the registration's Overview page, copy the Directory (tenant) ID and the Application (client) ID, and enter them under \"Your tenant\" at the top of FieldKit's setup page.",
            []),
    ];

    /// <summary>The steps as plain text, for sending to whoever administers Entra.</summary>
    public string PlainText
    {
        get
        {
            var lines = new List<string> { $"{AppName}: Entra app registration for Windows", "", ExistingRegistrationNote, "" };
            foreach (var step in Steps)
            {
                lines.Add($"{step.Number}. {step.Title}");
                lines.Add(step.Detail);
                lines.AddRange(step.Items.Select(item => $"   {item.Label}: {item.Value}"));
                lines.Add("");
            }
            lines.Add(NoSecretNote);
            lines.Add("");
            lines.Add(ConditionalAccessNote);
            lines.Add("");
            lines.Add($"Entra admin center: {AdminCenterUrl.OriginalString}");
            return string.Join('\n', lines);
        }
    }
}
