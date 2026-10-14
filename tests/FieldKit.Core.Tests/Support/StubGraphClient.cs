using FieldKit.Core.Graph;

namespace FieldKit.Core.Tests.Support;

/// <summary>Serves fixtures by Graph path, standing in for the whole client. Unknown paths answer 404.</summary>
internal sealed class StubGraphClient : IGraphClient
{
    private readonly Dictionary<string, StubReply> _routes;
    private readonly Func<GraphRequest, StubReply?>? _handler;
    private readonly Lock _lock = new();
    private readonly List<GraphRequest> _sent = [];

    /// <param name="routes">Graph path (e.g. "devices") to fixture name or error.</param>
    /// <param name="handler">Consulted first, for responses that depend on the query.</param>
    public StubGraphClient(Dictionary<string, StubReply> routes, Func<GraphRequest, StubReply?>? handler = null)
    {
        _routes = routes;
        _handler = handler;
    }

    public IReadOnlyList<GraphRequest> Sent
    {
        get
        {
            lock (_lock) return _sent.ToList();
        }
    }

    public IReadOnlyList<Uri> Requests => Sent.Select(request => request.Url).ToList();

    public Task<byte[]> SendAsync(GraphRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock) _sent.Add(request);
        var path = request.Url.AbsolutePath
            .Replace("/v1.0/", "", StringComparison.Ordinal)
            .Replace("/beta/", "", StringComparison.Ordinal);
        var reply = _handler?.Invoke(request) ?? _routes.GetValueOrDefault(path);
        return reply switch
        {
            null => Task.FromException<byte[]>(GraphException.NotFound()),
            { Error: { } error } => Task.FromException<byte[]>(error),
            { Fixture: "" or null } => Task.FromResult(Array.Empty<byte>()),
            { Fixture: { } name } => Task.FromResult(Support.Fixture.Data(name)),
        };
    }
}
