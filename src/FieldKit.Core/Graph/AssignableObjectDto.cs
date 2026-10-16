using System.Text.Json.Serialization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// Any assignable Intune object listed with <c>$expand=assignments</c>. Apps and most policies
/// use <c>displayName</c>; Settings Catalog uses <c>name</c>.
/// </summary>
internal sealed class AssignableObjectDto
{
    public required string Id { get; init; }

    [JsonPropertyName("@odata.type")]
    public string? Type { get; init; }

    public string? DisplayName { get; init; }

    public string? Name { get; init; }

    public string? Publisher { get; init; }

    public IReadOnlyList<AssignmentDto>? Assignments { get; init; }

    public AssignedItem ToDomain(AssignmentKind kind) =>
        new()
        {
            Id = Id,
            Kind = kind,
            Name = DisplayName ?? Name ?? "Unnamed",
            Detail = Detail(kind),
            Assignments = (Assignments ?? []).Select(assignment => assignment.ToDomain()).ToList(),
            TypeName = Type?.Replace("#microsoft.graph.", "", StringComparison.Ordinal) ?? "",
        };

    private string Detail(AssignmentKind kind)
    {
        if (kind is AssignmentKind.Remediation or AssignmentKind.Script)
        {
            return string.IsNullOrEmpty(Publisher) ? "" : Publisher;
        }
        return Type is null ? "" : AppTypeNames.Label(Type.Replace("#microsoft.graph.", "", StringComparison.Ordinal));
    }
}
