namespace FieldKit.Core.Graph;

/// <summary>A failed Graph call (ports <c>GraphError.swift</c>). The message is safe to show and log.</summary>
public sealed class GraphException : Exception
{
    private GraphException(GraphErrorKind kind, int? status = null, string? code = null, string? detail = null)
        : base(UserMessageFor(kind, status, code, detail))
    {
        Kind = kind;
        Status = status;
        Code = code;
        Detail = detail;
    }

    public GraphErrorKind Kind { get; }

    /// <summary>The HTTP status, for <see cref="GraphErrorKind.Server"/>.</summary>
    public int? Status { get; }

    /// <summary>Graph's error code, for <see cref="GraphErrorKind.Server"/> (e.g. "BadRequest") and sometimes <see cref="GraphErrorKind.Forbidden"/>.</summary>
    public string? Code { get; }

    /// <summary>What couldn't be read, for <see cref="GraphErrorKind.Decoding"/>.</summary>
    public string? Detail { get; }

    public string UserMessage => Message;

    public static GraphException Unauthorized() => new(GraphErrorKind.Unauthorized);

    /// <param name="code">Graph's error code, when it gave one (e.g. a tenant without the licence a feature needs).</param>
    public static GraphException Forbidden(string? code = null) => new(GraphErrorKind.Forbidden, code: code);

    public static GraphException NotFound() => new(GraphErrorKind.NotFound);

    public static GraphException Throttled() => new(GraphErrorKind.Throttled);

    public static GraphException Network() => new(GraphErrorKind.Network);

    public static GraphException Server(int status, string? code) => new(GraphErrorKind.Server, status, code);

    public static GraphException Decoding(string detail) => new(GraphErrorKind.Decoding, detail: detail);

    /// <summary>How <see cref="Detail"/> starts when Graph answered a read with no body at all.</summary>
    public const string EmptyResponseDetail = "empty response";

    /// <summary>Graph answered a read with an empty body (seen from the LAPS API for a device with no password).</summary>
    public bool IsEmptyResponse => Kind == GraphErrorKind.Decoding && Detail?.StartsWith(EmptyResponseDetail, StringComparison.Ordinal) == true;

    /// <summary>The same kind of error with the same details (exceptions compare by reference).</summary>
    public bool Matches(GraphException other) =>
        Kind == other.Kind && Status == other.Status && Code == other.Code && Detail == other.Detail;

    private static string UserMessageFor(GraphErrorKind kind, int? status, string? code, string? detail) =>
        kind switch
        {
            GraphErrorKind.Unauthorized => "Your sign-in expired. Sign in again.",
            GraphErrorKind.Forbidden => "Access denied: check your Intune role / scope tags.",
            GraphErrorKind.NotFound => "Not found.",
            GraphErrorKind.Throttled => "Graph is throttling requests. Wait a minute and retry.",
            GraphErrorKind.Network => "Couldn't reach Microsoft Graph. Check your connection and retry.",
            GraphErrorKind.Server when status >= 500 => "Microsoft Graph had a problem. Retry in a moment.",
            GraphErrorKind.Server => $"Graph rejected the request{(code is null ? "" : $" ({code})")}.",
            _ => $"Graph returned data FieldKit couldn't read ({detail}).",
        };
}
