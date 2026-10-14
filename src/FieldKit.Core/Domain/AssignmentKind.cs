namespace FieldKit.Core.Domain;

/// <summary>The kinds of Intune objects that can be assigned to groups, in display order.</summary>
public enum AssignmentKind
{
    App,
    ConfigurationProfile,
    SettingsCatalog,
    CompliancePolicy,
    Script,
    Remediation,
}
