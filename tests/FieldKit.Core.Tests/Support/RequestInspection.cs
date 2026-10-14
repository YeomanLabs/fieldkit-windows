using System.Text.Json;

namespace FieldKit.Core.Tests.Support;

/// <summary>Reads back what a request carried, for assertions.</summary>
internal static class RequestInspection
{
    /// <summary>A query parameter's decoded value, or null if it isn't there.</summary>
    public static string? QueryValue(Uri url, string name)
    {
        foreach (var item in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? item : item[..separator]);
            if (key == name) return separator < 0 ? "" : Uri.UnescapeDataString(item[(separator + 1)..]);
        }
        return null;
    }

    /// <summary>A flat JSON object body as a dictionary; null when there's no body.</summary>
    public static Dictionary<string, string>? JsonBody(byte[]? body)
    {
        if (body is null) return null;
        using var document = JsonDocument.Parse(body);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString() ?? "");
    }
}
