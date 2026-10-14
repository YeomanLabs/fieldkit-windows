namespace FieldKit.Core.Graph;

/// <summary>
/// Adds a device or user to a group (how FieldKit installs an app: the group is one the app
/// is assigned to as Required) or removes one. Only direct members can be removed.
/// </summary>
public sealed class GroupMembershipUpdates(IGraphClient client)
{
    /// <param name="directoryObjectId">The Entra object ID of the device or user (not the device ID).</param>
    public Task AddMemberAsync(string directoryObjectId, string groupId, CancellationToken cancellationToken = default) =>
        client.SendAsync(GraphRequest.Post($"groups/{groupId}/members/$ref", new Dictionary<string, string>
        {
            ["@odata.id"] = $"https://graph.microsoft.com/v1.0/directoryObjects/{directoryObjectId}",
        }), cancellationToken);

    public Task RemoveMemberAsync(string directoryObjectId, string groupId, CancellationToken cancellationToken = default) =>
        client.SendAsync(GraphRequest.Delete($"groups/{groupId}/members/{directoryObjectId}/$ref"), cancellationToken);
}
