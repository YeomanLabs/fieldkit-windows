namespace FieldKit.Core.Domain;

public readonly record struct AutopilotEnrollmentState
{
    private readonly string? _value;

    private AutopilotEnrollmentState(AutopilotEnrollmentStateKind kind, string? value = null)
    {
        Kind = kind;
        _value = value;
    }

    public AutopilotEnrollmentStateKind Kind { get; }

    /// <summary>Graph's value, for states FieldKit doesn't know (<see cref="AutopilotEnrollmentStateKind.Other"/>).</summary>
    public string Value => _value ?? "";

    public static AutopilotEnrollmentState Enrolled => new(AutopilotEnrollmentStateKind.Enrolled);

    public static AutopilotEnrollmentState NotContacted => new(AutopilotEnrollmentStateKind.NotContacted);

    public static AutopilotEnrollmentState PendingReset => new(AutopilotEnrollmentStateKind.PendingReset);

    public static AutopilotEnrollmentState Failed => new(AutopilotEnrollmentStateKind.Failed);

    public static AutopilotEnrollmentState Unknown => new(AutopilotEnrollmentStateKind.Unknown);

    public static AutopilotEnrollmentState Other(string value) => new(AutopilotEnrollmentStateKind.Other, value);

    public static AutopilotEnrollmentState FromRaw(string? rawValue) =>
        rawValue?.ToLowerInvariant() switch
        {
            "enrolled" => Enrolled,
            "notcontacted" => NotContacted,
            "pendingreset" => PendingReset,
            "failed" => Failed,
            "unknown" or null or "" => Unknown,
            _ => Other(rawValue),
        };

    public string DisplayName =>
        Kind switch
        {
            AutopilotEnrollmentStateKind.Enrolled => "Enrolled",
            AutopilotEnrollmentStateKind.NotContacted => "Not contacted",
            AutopilotEnrollmentStateKind.PendingReset => "Pending reset",
            AutopilotEnrollmentStateKind.Failed => "Failed",
            AutopilotEnrollmentStateKind.Other => Value,
            _ => "Unknown",
        };
}
