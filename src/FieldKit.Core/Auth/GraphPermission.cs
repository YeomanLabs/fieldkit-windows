namespace FieldKit.Core.Auth;

/// <summary>
/// Delegated Microsoft Graph permissions the app registration needs, for the setup guide.
/// Write access is limited to Autopilot, device actions, group membership (app installs) and
/// device extension attributes; keep it that way unless the owner approves more.
/// </summary>
/// <param name="IsOptional">Only one feature needs it, and everything else works without it.</param>
public sealed record GraphPermission(string Name, string Purpose, bool IsOptional = false)
{
    public static readonly IReadOnlyList<GraphPermission> All =
    [
        new("User.Read", "Sign-in and profile"),
        new("User.Read.All", "List and search people"),
        new("DeviceManagementManagedDevices.Read.All", "Intune devices"),
        new("DeviceManagementConfiguration.Read.All", "Compliance policy details, configuration status"),
        new("Device.Read.All", "Entra devices"),
        new("DeviceManagementServiceConfig.ReadWrite.All", "Autopilot lookup and pre-staging"),
        new("DeviceManagementManagedDevices.PrivilegedOperations.All", "Sync, restart, retire, run remediations"),
        new("DeviceLocalCredential.Read.All", "LAPS passwords"),
        new("BitlockerKey.Read.All", "BitLocker recovery keys"),
        new("GroupMember.Read.All", "Groups and memberships"),
        new("DeviceManagementApps.Read.All", "App assignments and install status"),
        new("DeviceManagementScripts.Read.All", "Scripts and remediations"),
        new("AuditLog.Read.All", "Sign-in logs: why was I blocked?"),
        new("Policy.Read.ConditionalAccess", "Which Conditional Access policies applied to a sign-in"),
        new("GroupMember.ReadWrite.All", "Install apps, add and remove group members"),
        // Approved 2026-10-13. Broad (FieldKit can do whatever the signed-in admin can in the
        // directory), but Graph offers nothing narrower for updating a device as a user.
        new("Directory.AccessAsUser.All", "Edit device extension attributes", IsOptional: true),
    ];

    /// <summary>
    /// Which of <see cref="All"/> a token's scopes include. Null when the scopes can't tell us
    /// (empty, or only <c>.default</c>), so the UI doesn't wrongly report everything missing.
    /// A ReadWrite grant also satisfies the matching Read permission.
    /// </summary>
    public static IReadOnlySet<string>? Granted(IEnumerable<string> scopes)
    {
        var names = scopes
            .Select(scope => (scope.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? scope).ToLowerInvariant())
            .ToHashSet();
        if (names.Count == 0 || names.SetEquals([".default"])) return null;
        return All
            .Select(permission => permission.Name)
            .Where(name =>
            {
                var lower = name.ToLowerInvariant();
                return names.Contains(lower) || names.Contains(lower.Replace(".read.", ".readwrite.", StringComparison.Ordinal));
            })
            .ToHashSet();
    }
}
