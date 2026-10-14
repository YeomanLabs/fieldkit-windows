namespace FieldKit.Core.Graph;

/// <summary>Graph's error response: <c>{ "error": { "code": "...", "message": "..." } }</c>.</summary>
internal sealed class GraphErrorBody
{
    internal sealed class DetailDto
    {
        public string? Code { get; init; }

        public string? Message { get; init; }
    }

    public required DetailDto Error { get; init; }
}
