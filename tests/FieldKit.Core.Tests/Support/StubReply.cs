using FieldKit.Core.Graph;

namespace FieldKit.Core.Tests.Support;

/// <summary>
/// What <see cref="StubGraphClient"/> answers for a path: a fixture name (empty for Graph's
/// 204 with no body) or an error. Converts from either, so routes read like the iOS tests.
/// </summary>
internal sealed record StubReply(string? Fixture, GraphException? Error)
{
    public static implicit operator StubReply(string fixture) => new(fixture, null);

    public static implicit operator StubReply(GraphException error) => new(null, error);
}
