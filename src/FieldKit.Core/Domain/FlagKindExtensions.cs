namespace FieldKit.Core.Domain;

public static class FlagKindExtensions
{
    public static string Title(this FlagKind kind) =>
        kind switch
        {
            FlagKind.NotInIntune => "Not enrolled",
            FlagKind.UnknownDevice => "Unknown device",
            FlagKind.Stale => "Stale",
            FlagKind.NoPrimaryUser => "No primary user",
            FlagKind.Noncompliant => "Not compliant",
            FlagKind.NameConvention => "Name convention",
            FlagKind.NameMismatch => "Name mismatch",
            FlagKind.EntraDisabled => "Entra disabled",
            FlagKind.NoEntraObject => "No Entra object",
            FlagKind.DuplicateIntuneRecords => "Duplicate Intune records",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    /// <summary>The spec's name for the flag, as iOS stores and exports it ("notInIntune").</summary>
    public static string RawValue(this FlagKind kind) =>
        kind switch
        {
            FlagKind.NotInIntune => "notInIntune",
            FlagKind.UnknownDevice => "unknownDevice",
            FlagKind.Stale => "stale",
            FlagKind.NoPrimaryUser => "noPrimaryUser",
            FlagKind.Noncompliant => "noncompliant",
            FlagKind.NameConvention => "nameConvention",
            FlagKind.NameMismatch => "nameMismatch",
            FlagKind.EntraDisabled => "entraDisabled",
            FlagKind.NoEntraObject => "noEntraObject",
            FlagKind.DuplicateIntuneRecords => "duplicateIntuneRecords",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static FlagKind? FromRawValue(string rawValue) =>
        Enum.GetValues<FlagKind>().Cast<FlagKind?>().FirstOrDefault(kind => kind!.Value.RawValue() == rawValue);
}
