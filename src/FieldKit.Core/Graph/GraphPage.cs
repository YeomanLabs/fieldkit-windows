namespace FieldKit.Core.Graph;

/// <summary>One page of a Graph collection, and where the next one is (null on the last page).</summary>
public sealed record GraphPage<T>(IReadOnlyList<T> Items, Uri? NextLink)
{
    public bool HasMore => NextLink is not null;
}
