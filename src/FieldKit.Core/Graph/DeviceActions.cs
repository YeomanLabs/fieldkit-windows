using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>Phase 3 remote actions. Each is a POST that Graph answers with 204 No Content.</summary>
public sealed class DeviceActions(IGraphClient client)
{
    public Task PerformAsync(DeviceAction action, string managedDeviceId, CancellationToken cancellationToken = default) =>
        client.SendAsync(GraphRequest.Post($"deviceManagement/managedDevices/{managedDeviceId}/{Endpoint(action)}"), cancellationToken);

    /// <summary>Runs a remediation (device health script) on a Windows device now. Beta only.</summary>
    public Task RunRemediationAsync(string scriptId, string managedDeviceId, CancellationToken cancellationToken = default) =>
        client.SendAsync(GraphRequest.Post(
            $"deviceManagement/managedDevices/{managedDeviceId}/initiateOnDemandProactiveRemediation",
            new Dictionary<string, string> { ["scriptPolicyId"] = scriptId },
            GraphVersion.Beta), cancellationToken);

    public static string Endpoint(DeviceAction action) =>
        action switch
        {
            DeviceAction.Sync => "syncDevice",
            DeviceAction.Restart => "rebootNow",
            DeviceAction.Retire => "retire",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
