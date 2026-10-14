namespace FieldKit.Core.Graph;

/// <summary>The network seam for <see cref="GraphClient"/>, so tests can script responses.</summary>
public interface IHttpTransport
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}
