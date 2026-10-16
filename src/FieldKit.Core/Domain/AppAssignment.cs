namespace FieldKit.Core.Domain;

/// <summary>One of an app's assignments, with its assignment filter (only beta Graph returns filters).</summary>
/// <param name="Intent">Graph's intent: "required", "available", "uninstall" ...</param>
public sealed record AppAssignment(AssignmentTarget Target, string Intent, string? FilterId = null, AssignmentFilterMode FilterMode = AssignmentFilterMode.None)
{
    public bool HasFilter => FilterMode != AssignmentFilterMode.None && !string.IsNullOrEmpty(FilterId);
}
