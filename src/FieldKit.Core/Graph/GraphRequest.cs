using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FieldKit.Core.Graph;

public sealed record GraphRequest
{
    public const string Host = "graph.microsoft.com";

    /// <summary>Characters left as they are in a path segment (RFC 3986 pchar); everything else is percent-encoded.</summary>
    private const string PathPunctuation = "-._~!$&'()*+,;=:@";

    /// <summary>
    /// Characters left as they are in a query name or value. Narrower than RFC 3986 allows:
    /// <c>&amp;</c>, <c>=</c> and <c>+</c> are encoded so they can't change how the query reads.
    /// </summary>
    private const string QueryPunctuation = "-._~!$'()*,;:@/?";

    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

    public required GraphMethod Method { get; init; }

    public required Uri Url { get; init; }

    public byte[]? Body { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = NoHeaders;

    public static GraphRequest Get(
        string path,
        IReadOnlyList<(string Name, string Value)>? query = null,
        IReadOnlyDictionary<string, string>? headers = null,
        GraphVersion version = GraphVersion.V1) =>
        new()
        {
            Method = GraphMethod.Get,
            Url = BuildUrl(path, query ?? [], version),
            Headers = Copy(headers),
        };

    /// <summary>For following <c>@odata.nextLink</c>, which Graph returns as an absolute URL.</summary>
    public static GraphRequest Get(Uri absolute, IReadOnlyDictionary<string, string>? headers = null) =>
        new() { Method = GraphMethod.Get, Url = absolute, Headers = Copy(headers) };

    /// <summary>A POST with an optional JSON object body.</summary>
    public static GraphRequest Post(string path, IReadOnlyDictionary<string, string>? json = null, GraphVersion version = GraphVersion.V1) =>
        new()
        {
            Method = GraphMethod.Post,
            Url = BuildUrl(path, [], version),
            Body = json is null ? null : JsonObject(json),
        };

    /// <summary>A PATCH with a JSON body the caller has written (it may hold nested objects and nulls).</summary>
    public static GraphRequest Patch(string path, byte[] json) =>
        new()
        {
            Method = GraphMethod.Patch,
            Url = BuildUrl(path, [], GraphVersion.V1),
            Body = json,
        };

    /// <summary>A POST that reads: one of Intune's report actions (<c>deviceManagement/reports/*</c>).</summary>
    public static GraphRequest PostJson(string path, byte[] json, GraphVersion version = GraphVersion.V1) =>
        new()
        {
            Method = GraphMethod.Post,
            Url = BuildUrl(path, [], version),
            Body = json,
        };

    public static GraphRequest Delete(string path) =>
        new() { Method = GraphMethod.Delete, Url = BuildUrl(path, [], GraphVersion.V1) };

    private static Uri BuildUrl(string path, IReadOnlyList<(string Name, string Value)> query, GraphVersion version)
    {
        var url = new StringBuilder("https://").Append(Host).Append('/')
            .Append(version == GraphVersion.Beta ? "beta" : "v1.0").Append('/')
            .AppendJoin('/', path.Split('/').Select(segment => Escape(segment, PathPunctuation)));
        if (query.Count > 0)
        {
            url.Append('?').AppendJoin('&', query.Select(item =>
                Escape(item.Name, QueryPunctuation) + "=" + Escape(item.Value, QueryPunctuation)));
        }
        return new Uri(url.ToString(), UriKind.Absolute);
    }

    private static string Escape(string text, string punctuation)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            var character = (char)value;
            if (value < 0x80 && (char.IsAsciiLetterOrDigit(character) || punctuation.Contains(character)))
            {
                escaped.Append(character);
            }
            else
            {
                escaped.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return escaped.ToString();
    }

    /// <summary>A flat JSON object with sorted keys, so bodies are stable for tests.</summary>
    private static byte[] JsonObject(IReadOnlyDictionary<string, string> fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in fields.OrderBy(field => field.Key, StringComparer.Ordinal))
            {
                writer.WriteString(key, value);
            }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string>? headers) =>
        headers is null || headers.Count == 0
            ? NoHeaders
            : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
}
