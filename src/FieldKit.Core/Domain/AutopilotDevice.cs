namespace FieldKit.Core.Domain;

public sealed record AutopilotDevice
{
    public required string Id { get; init; }

    public string SerialNumber { get; init; } = "";

    public string GroupTag { get; init; } = "";

    /// <summary>UPN of the user assigned to the Autopilot record (empty if none).</summary>
    public string AssignedUser { get; init; } = "";

    public AutopilotEnrollmentState EnrollmentState { get; init; } = AutopilotEnrollmentState.Unknown;

    public DateTimeOffset? LastContacted { get; init; }

    public string Model { get; init; } = "";

    public string Manufacturer { get; init; } = "";
}
