namespace FieldKit.Core.Graph;

public static class GraphFeatureExtensions
{
    public static string Permission(this GraphFeature feature) =>
        feature switch
        {
            GraphFeature.PeopleSearch => "User.Read.All",
            GraphFeature.ComplianceDetails => "DeviceManagementConfiguration.Read.All",
            GraphFeature.DeviceActions => "DeviceManagementManagedDevices.PrivilegedOperations.All",
            GraphFeature.LocalAdminPassword => "DeviceLocalCredential.Read.All",
            GraphFeature.BitLockerKeys => "BitlockerKey.Read.All",
            GraphFeature.AutopilotEdit => "DeviceManagementServiceConfig.ReadWrite.All",
            GraphFeature.Groups => "GroupMember.Read.All",
            GraphFeature.AppAssignments => "DeviceManagementApps.Read.All",
            GraphFeature.PolicyAssignments => "DeviceManagementConfiguration.Read.All",
            GraphFeature.ScriptAssignments => "DeviceManagementScripts.Read.All",
            GraphFeature.RunRemediation => "DeviceManagementManagedDevices.PrivilegedOperations.All",
            GraphFeature.AppInstall or GraphFeature.GroupMembership => "GroupMember.ReadWrite.All",
            // Graph has no narrower delegated permission for updating a device.
            GraphFeature.ExtensionAttributes => "Directory.AccessAsUser.All",
            GraphFeature.SignIns => "AuditLog.Read.All",
            GraphFeature.AppInstallStatus => "DeviceManagementApps.Read.All",
            GraphFeature.ConfigurationStatus => "DeviceManagementConfiguration.Read.All",
            GraphFeature.AssignmentFilters => "DeviceManagementConfiguration.Read.All",
            _ => throw new ArgumentOutOfRangeException(nameof(feature)),
        };

    public static string RoleHint(this GraphFeature feature) =>
        feature switch
        {
            GraphFeature.PeopleSearch => "a directory account that can read users",
            GraphFeature.ComplianceDetails => "an Intune role that can read device compliance",
            GraphFeature.DeviceActions => "an Intune role with remote tasks, such as Help Desk Operator",
            GraphFeature.LocalAdminPassword => "an Entra role that can read LAPS passwords, such as Cloud Device Administrator",
            GraphFeature.BitLockerKeys => "an Entra role that can read BitLocker keys, such as Cloud Device Administrator or Helpdesk Administrator",
            GraphFeature.AutopilotEdit => "an Intune role that can update Autopilot devices, such as Policy and Profile Manager",
            GraphFeature.Groups => "a directory account that can read groups",
            GraphFeature.AppAssignments => "an Intune role that can read apps",
            GraphFeature.PolicyAssignments => "an Intune role that can read device configurations",
            GraphFeature.ScriptAssignments => "an Intune role that can read scripts and remediations",
            GraphFeature.RunRemediation => "an Intune role that can run remediations, and a Windows licence that includes Remediations",
            GraphFeature.AppInstall or GraphFeature.GroupMembership =>
                "an Entra role that can manage this group's members (such as Groups Administrator or Intune Administrator), or ownership of the group",
            GraphFeature.ExtensionAttributes => "the Intune Administrator role (or a custom Entra role that can update device extension attributes)",
            GraphFeature.SignIns => "an Entra role that can read sign-in logs, such as Reports Reader, Security Reader or Global Reader",
            GraphFeature.AppInstallStatus => "an Intune role that can read apps and their install status, such as Read Only Operator",
            GraphFeature.ConfigurationStatus => "an Intune role that can read device configurations, such as Read Only Operator",
            GraphFeature.AssignmentFilters => "an Intune role that can read assignment filters, such as Read Only Operator",
            _ => throw new ArgumentOutOfRangeException(nameof(feature)),
        };

    public static string AccessDeniedMessage(this GraphFeature feature) =>
        $"Access denied. This needs the {feature.Permission()} permission with admin consent on the app registration, and {feature.RoleHint()}.";

    /// <summary>Graph's 403 code when the tenant has no Entra ID P1 or P2, which sign-in logs need.</summary>
    public const string NonPremiumTenant = "Authentication_RequestFromNonPremiumTenantOrB2CTenant";

    /// <summary>User-facing text for an error from this feature's Graph calls.</summary>
    public static string Message(this GraphFeature feature, GraphException error) =>
        feature == GraphFeature.SignIns && error.Code == NonPremiumTenant ? "Sign-in logs need Microsoft Entra ID P1 or P2 in this tenant."
        : error.Kind == GraphErrorKind.Forbidden ? feature.AccessDeniedMessage() : error.UserMessage;
}
