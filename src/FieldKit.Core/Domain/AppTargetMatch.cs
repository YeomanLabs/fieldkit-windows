namespace FieldKit.Core.Domain;

/// <summary>One of an app's assignments that reaches this device, and how.</summary>
/// <param name="Via">"All devices", "All users", "CTS Laptops (device group)".</param>
/// <param name="ThroughUser">It reaches the device through its user rather than the device itself.</param>
/// <param name="Filter">The assignment's filter, when it has one and FieldKit could read it.</param>
public sealed record AppTargetMatch(AppAssignment Assignment, string Via, bool ThroughUser, AssignmentFilter? Filter)
{
    public bool IsExclusion => Assignment.Target.Kind == AssignmentTargetKind.ExcludedGroup;

    /// <summary>"Required", "Available", "Uninstall"; "Excluded" for an exclusion.</summary>
    public string IntentTitle => IsExclusion ? "Excluded" : AssignedItem.IntentTitle(Assignment.Intent) ?? "Assigned";

    public bool IsUninstall => string.Equals(Assignment.Intent, "uninstall", StringComparison.OrdinalIgnoreCase);

    public bool IsRequired => string.Equals(Assignment.Intent, "required", StringComparison.OrdinalIgnoreCase);

    /// <summary>"Only devices that match “Contoso desktops”: (device.model -startsWith "ThinkCentre")", or empty.</summary>
    public string FilterText
    {
        get
        {
            if (!Assignment.HasFilter) return "";
            var name = Filter is { } known ? $"“{known.Name}”" : "a filter FieldKit couldn't read";
            var rule = Filter is { Rule.Length: > 0 } withRule ? $": {withRule.Rule}" : "";
            return Assignment.FilterMode == AssignmentFilterMode.Exclude
                ? $"Except devices that match {name}{rule}"
                : $"Only devices that match {name}{rule}";
        }
    }
}
