namespace FieldKit.Core.Graph;

/// <summary>
/// Features beyond the phase 1 lookups, with what each needs when Graph says 403
/// (<see cref="GraphFeatureExtensions"/>). Used to turn "Access denied" into something an admin can act on.
/// </summary>
public enum GraphFeature
{
    PeopleSearch,
    ComplianceDetails,
    DeviceActions,
    LocalAdminPassword,
    BitLockerKeys,
    AutopilotEdit,
    Groups,
    AppAssignments,
    PolicyAssignments,
    ScriptAssignments,
    RunRemediation,
    AppInstall,
    GroupMembership,
    ExtensionAttributes,
    SignIns,
    AppInstallStatus,
    ConfigurationStatus,
    AssignmentFilters,
}
