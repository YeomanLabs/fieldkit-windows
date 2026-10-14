namespace FieldKit.Core.Domain;

/// <summary>
/// The flag rules, in the order they're shown at equal severity. <see cref="FlagKindExtensions.RawValue"/>
/// gives the names used in the spec, the CSV export and saved scan records.
/// </summary>
public enum FlagKind
{
    NotInIntune,
    UnknownDevice,
    Stale,
    NoPrimaryUser,
    Noncompliant,
    NameConvention,
    NameMismatch,
    EntraDisabled,
    NoEntraObject,
    DuplicateIntuneRecords,
}
