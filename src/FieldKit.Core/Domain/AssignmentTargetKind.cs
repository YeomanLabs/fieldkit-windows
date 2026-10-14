namespace FieldKit.Core.Domain;

/// <summary>The cases of <see cref="AssignmentTarget"/>.</summary>
public enum AssignmentTargetKind
{
    Other,
    AllDevices,
    AllUsers,
    Group,
    ExcludedGroup,
}
