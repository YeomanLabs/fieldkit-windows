namespace FieldKit.Core.Graph;

/// <summary>A sign-in's <c>location</c>.</summary>
internal sealed class SignInLocationDto
{
    public string? City { get; init; }

    public string? State { get; init; }

    public string? CountryOrRegion { get; init; }

    /// <summary>"Redmond, Washington, US", skipping the parts Graph left empty.</summary>
    public string ToText() => string.Join(", ", new[] { City, State, CountryOrRegion }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
