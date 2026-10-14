using System.Net;

namespace FieldKit.Core.Graph;

/// <summary>The real transport: one HttpClient shared by the whole app.</summary>
public sealed class HttpClientTransport : IHttpTransport
{
    private static readonly HttpClient Shared = new(new SocketsHttpHandler
    {
        // Recycle connections so DNS changes are picked up in a long-running session.
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AutomaticDecompression = DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    private readonly HttpClient _client;

    public HttpClientTransport(HttpClient? client = null)
    {
        _client = client ?? Shared;
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
}
