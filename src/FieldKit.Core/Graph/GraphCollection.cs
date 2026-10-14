using System.Text.Json.Serialization;

namespace FieldKit.Core.Graph;

/// <summary>One page of a Graph collection.</summary>
internal sealed class GraphCollection<TItem>
{
    public required IReadOnlyList<TItem> Value { get; init; }

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; init; }
}
