using System.Text;
using FieldKit.Core.Graph;

namespace FieldKit.Core.Tests.Support;

/// <summary>
/// Answers each request with JSON (or an exception) from a script, for paging tests that need nextLinks.
/// With <paramref name="hold"/>, answers wait for it, so a test can act while a request is in flight.
/// </summary>
internal sealed class ScriptedGraphClient(Func<GraphRequest, string> respond, Task? hold = null) : IGraphClient
{
    private readonly Lock _lock = new();
    private readonly List<GraphRequest> _sent = [];

    public IReadOnlyList<GraphRequest> Sent
    {
        get
        {
            lock (_lock) return _sent.ToList();
        }
    }

    public async Task<byte[]> SendAsync(GraphRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock) _sent.Add(request);
        if (hold is not null) await hold.WaitAsync(cancellationToken);
        return await Answer(request);
    }

    private Task<byte[]> Answer(GraphRequest request)
    {
        try
        {
            return Task.FromResult(Encoding.UTF8.GetBytes(respond(request)));
        }
        catch (Exception error)
        {
            return Task.FromException<byte[]>(error);
        }
    }

    /// <summary>A collection page of objects with these IDs and display names, and an optional next link.</summary>
    public static string Page(IEnumerable<string> names, string? nextLink = null, string nameField = "displayName")
    {
        var items = names.Select(name => $$"""{"id":"{{name.ToLowerInvariant()}}","{{nameField}}":"{{name}}"}""");
        var next = nextLink is null ? "" : $$""","@odata.nextLink":"{{nextLink}}" """;
        return $$"""{"value":[{{string.Join(",", items)}}]{{next}}}""";
    }
}
