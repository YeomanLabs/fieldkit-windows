namespace FieldKit.Core.Domain;

/// <param name="Detail">One line explaining why the flag fired, shown under the chips.</param>
public sealed record Flag(FlagKind Kind, FlagSeverity Severity, string Detail);
