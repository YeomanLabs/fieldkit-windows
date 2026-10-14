using System.Net;
using FieldKit.Core.Graph;

namespace FieldKit.Core.Tests.Support;

/// <summary>Replays scripted responses in order and records what was sent.</summary>
internal sealed class ScriptedTransport : IHttpTransport
{
    internal sealed record Reply(int Code, byte[] Body, IReadOnlyDictionary<string, string> Headers, bool Fails)
    {
        public static Reply Status(int code, byte[]? body = null, Dictionary<string, string>? headers = null) =>
            new(code, body ?? [], headers ?? [], Fails: false);

        /// <summary>The network fails before a response arrives.</summary>
        public static Reply Failure() => new(0, [], new Dictionary<string, string>(), Fails: true);
    }

    /// <summary>A copy of what was sent, taken before the client disposes the message.</summary>
    internal sealed record SentRequest(string Method, Uri Url, IReadOnlyDictionary<string, string> Headers, byte[]? Body);

    private readonly Queue<Reply> _replies;

    public ScriptedTransport(params Reply[] replies)
    {
        _replies = new Queue<Reply>(replies);
    }

    public List<SentRequest> Sent { get; } = [];

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }
        byte[]? body = null;
        if (request.Content is { } content)
        {
            foreach (var header in content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
            body = await content.ReadAsByteArrayAsync(cancellationToken);
        }
        Sent.Add(new SentRequest(request.Method.Method, request.RequestUri!, headers, body));

        if (!_replies.TryDequeue(out var reply) || reply.Fails)
        {
            throw new HttpRequestException("Scripted network failure.");
        }
        var response = new HttpResponseMessage((HttpStatusCode)reply.Code)
        {
            Content = new ByteArrayContent(reply.Body),
            RequestMessage = request,
        };
        foreach (var (name, value) in reply.Headers)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }
        return response;
    }
}
