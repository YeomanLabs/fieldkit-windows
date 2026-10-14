using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

public static class AssignmentKindFeatureExtensions
{
    /// <summary>The feature whose permission this kind's catalog needs, for 403 messages.</summary>
    public static GraphFeature Feature(this AssignmentKind kind) =>
        kind switch
        {
            AssignmentKind.App => GraphFeature.AppAssignments,
            AssignmentKind.ConfigurationProfile or AssignmentKind.SettingsCatalog or AssignmentKind.CompliancePolicy => GraphFeature.PolicyAssignments,
            AssignmentKind.Script or AssignmentKind.Remediation => GraphFeature.ScriptAssignments,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
