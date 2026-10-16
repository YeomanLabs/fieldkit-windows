using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary><c>beta/deviceManagement/assignmentFilters/{id}</c>.</summary>
internal sealed class AssignmentFilterDto
{
    public const string SelectFields = "id,displayName,rule,platform";

    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Rule { get; init; }

    public string? Platform { get; init; }

    public AssignmentFilter ToDomain() => new(Id, DisplayName ?? "Unnamed filter", Rule ?? "", Platform ?? "");
}
