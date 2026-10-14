namespace FieldKit.Core.Graph;

/// <summary><c>GET /organization</c> item.</summary>
internal sealed class OrganizationDto
{
    public required string Id { get; init; }

    public string? DisplayName { get; init; }
}
