namespace FieldKit.Core.Graph;

/// <summary>
/// Phase 2 Autopilot pre-staging. Intune applies these asynchronously; the record can take
/// a few minutes to show the change.
/// </summary>
public sealed class AutopilotUpdates(IGraphClient client)
{
    /// <summary>Sets (or, with an empty string, clears) the group tag.</summary>
    public Task SetGroupTagAsync(string tag, string identityId, CancellationToken cancellationToken = default) =>
        client.SendAsync(GraphRequest.Post(Path(identityId, "updateDeviceProperties"),
            new Dictionary<string, string> { ["groupTag"] = tag }), cancellationToken);

    /// <param name="displayName">The friendly name shown during setup (Graph's addressableUserName).</param>
    public Task AssignUserAsync(string userPrincipalName, string displayName, string identityId, CancellationToken cancellationToken = default) =>
        client.SendAsync(GraphRequest.Post(Path(identityId, "assignUserToDevice"), new Dictionary<string, string>
        {
            ["userPrincipalName"] = userPrincipalName,
            ["addressableUserName"] = displayName,
        }), cancellationToken);

    public Task UnassignUserAsync(string identityId, CancellationToken cancellationToken = default) =>
        client.SendAsync(GraphRequest.Post(Path(identityId, "unassignUserFromDevice")), cancellationToken);

    private static string Path(string identityId, string action) =>
        $"deviceManagement/windowsAutopilotDeviceIdentities/{identityId}/{action}";
}
