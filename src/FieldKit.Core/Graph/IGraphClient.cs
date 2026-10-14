namespace FieldKit.Core.Graph;

/// <summary>
/// Everything that talks to Graph goes through this (ports <c>GraphClientProtocol</c>):
/// <see cref="GraphClient"/> for a tenant, <see cref="Demo.DemoGraphClient"/> for demo mode, and
/// stubs in tests. <see cref="GraphClientExtensions"/> adds typed reads on top.
/// </summary>
public interface IGraphClient
{
    /// <summary>Sends the request with auth, retry and throttling handled; returns the 2xx body.</summary>
    Task<byte[]> SendAsync(GraphRequest request, CancellationToken cancellationToken = default);
}
