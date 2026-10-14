namespace FieldKit.Core.Domain;

/// <summary>What a group member is: a person, a device, a nested group or something else.</summary>
public abstract record GroupMemberKind
{
    private GroupMemberKind()
    {
    }

    public sealed record User(string UserPrincipalName) : GroupMemberKind;

    /// <param name="DeviceId">The Entra device ID, used to find the Intune record.</param>
    public sealed record Device(string? DeviceId, string OperatingSystem) : GroupMemberKind;

    public sealed record Group : GroupMemberKind;

    public sealed record Other : GroupMemberKind;
}
