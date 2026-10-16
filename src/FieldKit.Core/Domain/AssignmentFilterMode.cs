namespace FieldKit.Core.Domain;

/// <summary>How an assignment uses its assignment filter: devices that match are included, or excluded.</summary>
public enum AssignmentFilterMode
{
    None,
    Include,
    Exclude,
}
