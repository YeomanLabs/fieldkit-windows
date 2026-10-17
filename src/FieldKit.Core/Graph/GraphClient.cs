using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using FieldKit.Core.Auth;
using FieldKit.Core.Services;

namespace FieldKit.Core.Graph;

/// <summary>Sends Graph requests for the signed-in user: auth, one refresh on 401/403, throttling, errors.</summary>
public sealed class GraphClient : IGraphClient
{
    public const int MaxThrottleRetries = 3;

    /// <summary>Longest single wait honoured from <c>Retry-After</c>; anything longer surfaces as throttled.</summary>
    public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(30);

    /// <summary>
    /// "FieldKit/1.0.1 (Windows 10.0.26200)". The LAPS and BitLocker APIs require a User-Agent and
    /// answer 400 invalid_request without one; .NET sends none by default (iOS's URLSession does).
    /// </summary>
    public static readonly string UserAgent =
        $"FieldKit/{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0"} (Windows {Environment.OSVersion.Version.ToString(3)})";

    private readonly ITokenProvider _tokens;
    private readonly IHttpTransport _transport;
    private readonly Func<TimeSpan, CancellationToken, Task> _sleep;

    public GraphClient(
        ITokenProvider tokens,
        IHttpTransport? transport = null,
        Func<TimeSpan, CancellationToken, Task>? sleep = null)
    {
        _tokens = tokens;
        _transport = transport ?? new HttpClientTransport();
        _sleep = sleep ?? Task.Delay;
    }

    public async Task<byte[]> SendAsync(GraphRequest request, CancellationToken cancellationToken = default)
    {
        // The token goes only to Graph, whatever URL a response hands back.
        if (request.Url.Scheme != Uri.UriSchemeHttps || request.Url.Host != GraphRequest.Host)
        {
            throw new InvalidOperationException("GraphClient only sends requests to Microsoft Graph.");
        }

        var refreshedToken = false;
        var throttleRetries = 0;
        // Set when the refresh is only a second try after a 403 (a permission granted since sign-in).
        var forbidden = false;
        string? forbiddenCode = null;

        while (true)
        {
            string token;
            try
            {
                token = await _tokens.GetAccessTokenAsync(refreshedToken, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthException error)
            {
                if (error.Kind == AuthErrorKind.Network) throw GraphException.Network();
                // After a 403 the token was fine; Windows just can't refresh it without the user
                // (a permission newly added to the app registration, MFA). Report the 403 rather
                // than signing out (reported on 1.5.0-test.3).
                throw forbidden ? GraphException.Forbidden(forbiddenCode) : GraphException.Unauthorized();
            }

            using var message = BuildMessage(request, token);
            int status;
            byte[] data;
            string? retryAfter;
            string requestId;
            string? contentType;
            try
            {
                using var response = await _transport.SendAsync(message, cancellationToken).ConfigureAwait(false);
                status = (int)response.StatusCode;
                data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                retryAfter = Header(response, "Retry-After");
                requestId = Header(response, "request-id") ?? "none";
                contentType = response.Content.Headers.ContentType?.MediaType;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException)
            {
                // OperationCanceledException without our token being cancelled is HttpClient's timeout.
                throw GraphException.Network();
            }

            switch (status)
            {
                case >= 200 and < 300:
                    CheckReadable(data, request.Method, status, contentType, requestId);
                    return data;
                // A 403 can also mean an admin granted the permission after this token was
                // issued, so one refresh is tried before reporting either.
                case 401 or 403 when !refreshedToken:
                    refreshedToken = true;
                    forbidden = status == 403;
                    forbiddenCode = forbidden ? ErrorCode(data) : null;
                    break;
                case 401:
                    throw GraphException.Unauthorized();
                case 403:
                    throw GraphException.Forbidden(ErrorCode(data));
                case 404:
                    throw GraphException.NotFound();
                case 429 or 503:
                    if (throttleRetries >= MaxThrottleRetries) throw GraphException.Throttled();
                    var wait = RetryDelay(retryAfter, throttleRetries);
                    if (wait > MaxRetryAfter) throw GraphException.Throttled();
                    throttleRetries++;
                    Debug.WriteLine($"Graph throttled ({status}); retry {throttleRetries}");
                    await _sleep(wait, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    var code = ErrorCode(data);
                    Debug.WriteLine($"Graph error {status} {code ?? "-"}");
                    throw GraphException.Server(status, code, requestId == "none" ? null : requestId);
            }
        }
    }

    /// <summary>
    /// A GET should always answer with JSON. When it doesn't, report what came back (status,
    /// content type, size, request ID; never the body, which may hold a secret) so the
    /// cause can be traced with Microsoft.
    /// </summary>
    internal static void CheckReadable(byte[] data, GraphMethod method, int status, string? contentType, string requestId)
    {
        if (method != GraphMethod.Get) return;
        if (data.Length == 0)
        {
            throw GraphException.Decoding($"{GraphException.EmptyResponseDetail}, HTTP {status}, request-id {requestId}");
        }
        try
        {
            using var document = JsonDocument.Parse(GraphJson.Normalized(data));
        }
        catch (JsonException)
        {
            var detail = $"not JSON: HTTP {status}, {contentType ?? "no content type"}, {data.Length} bytes, request-id {requestId}";
            Debug.WriteLine(detail);
            throw GraphException.Decoding(detail);
        }
    }

    /// <summary><c>Retry-After</c> in seconds when Graph sends it, otherwise exponential backoff (1s, 2s, 4s).</summary>
    public static TimeSpan RetryDelay(string? header, int attempt)
    {
        if (int.TryParse(header?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }
        return TimeSpan.FromSeconds(1 << attempt);
    }

    private static HttpRequestMessage BuildMessage(GraphRequest request, string token)
    {
        var method = request.Method switch
        {
            GraphMethod.Get => HttpMethod.Get,
            GraphMethod.Post => HttpMethod.Post,
            GraphMethod.Patch => HttpMethod.Patch,
            GraphMethod.Delete => HttpMethod.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
        var message = new HttpRequestMessage(method, request.Url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (request.Body is { } body)
        {
            message.Content = new ByteArrayContent(body);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        foreach (var (field, value) in request.Headers)
        {
            message.Headers.Remove(field);
            message.Headers.TryAddWithoutValidation(field, value);
        }
        return message;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string? ErrorCode(byte[] data)
    {
        try
        {
            return GraphJson.Decode<GraphErrorBody>(data).Error.Code;
        }
        catch (GraphException)
        {
            return null;
        }
    }
}
