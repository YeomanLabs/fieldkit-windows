namespace FieldKit.Core.Domain;

public static class AssignmentKindExtensions
{
    public static string Title(this AssignmentKind kind) =>
        kind switch
        {
            AssignmentKind.App => "Apps",
            AssignmentKind.ConfigurationProfile => "Configuration profiles",
            AssignmentKind.SettingsCatalog => "Settings catalog",
            AssignmentKind.CompliancePolicy => "Compliance policies",
            AssignmentKind.Script => "Scripts",
            AssignmentKind.Remediation => "Remediations",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
