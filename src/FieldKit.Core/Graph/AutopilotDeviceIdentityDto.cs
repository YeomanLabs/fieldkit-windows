using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary><c>GET /deviceManagement/windowsAutopilotDeviceIdentities</c> item.</summary>
internal sealed class AutopilotDeviceIdentityDto
{
    public required string Id { get; init; }

    public string? SerialNumber { get; init; }

    public string? GroupTag { get; init; }

    public string? UserPrincipalName { get; init; }

    public string? EnrollmentState { get; init; }

    public DateTimeOffset? LastContactedDateTime { get; init; }

    public string? Model { get; init; }

    public string? Manufacturer { get; init; }

    public AutopilotDevice ToDomain() =>
        new()
        {
            Id = Id,
            SerialNumber = SerialNumber ?? "",
            GroupTag = GroupTag ?? "",
            AssignedUser = UserPrincipalName ?? "",
            EnrollmentState = AutopilotEnrollmentState.FromRaw(EnrollmentState),
            LastContacted = GraphJson.Meaningful(LastContactedDateTime),
            Model = Model ?? "",
            Manufacturer = Manufacturer ?? "",
        };
}
