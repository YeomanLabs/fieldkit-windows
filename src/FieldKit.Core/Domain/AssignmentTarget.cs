namespace FieldKit.Core.Domain;

/// <summary>Who an Intune assignment is aimed at.</summary>
public readonly record struct AssignmentTarget
{
    private AssignmentTarget(AssignmentTargetKind kind, string? groupId = null)
    {
        Kind = kind;
        GroupId = groupId;
    }

    public AssignmentTargetKind Kind { get; }

    /// <summary>The group for <see cref="AssignmentTargetKind.Group"/> and <see cref="AssignmentTargetKind.ExcludedGroup"/>.</summary>
    public string? GroupId { get; }

    public static AssignmentTarget AllDevices => new(AssignmentTargetKind.AllDevices);

    public static AssignmentTarget AllUsers => new(AssignmentTargetKind.AllUsers);

    public static AssignmentTarget Other => new(AssignmentTargetKind.Other);

    public static AssignmentTarget Group(string id) => new(AssignmentTargetKind.Group, id);

    public static AssignmentTarget ExcludedGroup(string id) => new(AssignmentTargetKind.ExcludedGroup, id);
}
