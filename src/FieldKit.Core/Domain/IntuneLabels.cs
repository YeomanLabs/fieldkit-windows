using System.Text;

namespace FieldKit.Core.Domain;

/// <summary>Readable names for Intune enum values shown on the inventory tab.</summary>
public static class IntuneLabels
{
    public static string ManagementAgent(string raw) =>
        raw.ToLowerInvariant() switch
        {
            "mdm" => "Intune MDM",
            "eas" => "Exchange ActiveSync",
            "easmdm" => "Intune MDM + Exchange ActiveSync",
            "intuneclient" => "Intune PC agent",
            "configurationmanagerclient" => "Configuration Manager",
            "configurationmanagerclientmdm" => "Co-managed (Configuration Manager + Intune)",
            "configurationmanagerclientmdmeas" => "Co-managed (Configuration Manager + Intune + EAS)",
            "jamf" => "Jamf",
            "googleclouddevicepolicycontroller" => "Google device policy",
            _ => Words(raw),
        };

    public static string EnrollmentType(string raw) =>
        raw.ToLowerInvariant() switch
        {
            "windowsazureadjoin" => "Entra join",
            "windowsazureadjoinusingdeviceauth" => "Entra join (device authentication)",
            "windowsautoenrollment" => "Windows automatic enrollment",
            "windowsbulkazuredomainjoin" => "Bulk enrollment (provisioning package)",
            "windowsbulkuserless" => "Bulk enrollment (no user)",
            "windowscomanagement" => "Co-management",
            "userenrollment" => "User enrollment",
            "deviceenrollmentmanager" => "Device enrollment manager",
            "applebulkwithuser" => "Apple automated enrollment (with user)",
            "applebulkwithoutuser" => "Apple automated enrollment (no user)",
            "appleuserenrollment" => "Apple User Enrollment",
            "appleuserenrollmentwithserviceaccount" => "Apple User Enrollment (service account)",
            _ => Words(raw),
        };

    /// <summary>
    /// "deviceEnrollmentManager" → "Device Enrollment Manager", keeping acronyms:
    /// "windowsAzureADJoin" → "Windows Azure AD Join".
    /// </summary>
    public static string Words(string camel)
    {
        var result = new StringBuilder();
        for (var index = 0; index < camel.Length; index++)
        {
            var character = camel[index];
            if (index > 0 && char.IsUpper(character))
            {
                var previous = camel[index - 1];
                var nextIsLower = index + 1 < camel.Length && char.IsLower(camel[index + 1]);
                if (char.IsLower(previous) || char.IsNumber(previous) || (char.IsUpper(previous) && nextIsLower))
                {
                    result.Append(' ');
                }
            }
            result.Append(character);
        }
        if (result.Length == 0) return "";
        result[0] = char.ToUpperInvariant(result[0]);
        return result.ToString();
    }
}
