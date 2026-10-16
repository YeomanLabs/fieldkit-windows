namespace FieldKit.Core.Domain;

/// <summary>Which of an app's assignments reach a device, and which exclude it.</summary>
public sealed record AppTargeting(IReadOnlyList<AppTargetMatch> Includes, IReadOnlyList<AppTargetMatch> Excludes)
{
    /// <summary>
    /// Works out the assignments that reach a device from its groups and its primary user's.
    /// Filters are attached, not evaluated: Graph has no call that says whether one matched.
    /// </summary>
    /// <param name="filters">Filters by ID; missing ones show as unreadable.</param>
    public static AppTargeting Resolve(IEnumerable<AppAssignment> assignments, DeviceMembership membership, IReadOnlyDictionary<string, AssignmentFilter> filters)
    {
        var includes = new List<AppTargetMatch>();
        var excludes = new List<AppTargetMatch>();
        foreach (var assignment in assignments)
        {
            var filter = assignment.FilterId is { } id && filters.TryGetValue(id, out var known) ? known : null;
            var target = assignment.Target;
            switch (target.Kind)
            {
                case AssignmentTargetKind.AllDevices:
                    includes.Add(new AppTargetMatch(assignment, "All devices", false, filter));
                    break;
                case AssignmentTargetKind.AllUsers when membership.HasUser:
                    includes.Add(new AppTargetMatch(assignment, "All users", true, filter));
                    break;
                case AssignmentTargetKind.Group or AssignmentTargetKind.ExcludedGroup:
                    if (Via(target.GroupId, membership) is not { } via) continue;
                    var match = new AppTargetMatch(assignment, via.Text, via.ThroughUser, filter);
                    (target.Kind == AssignmentTargetKind.Group ? includes : excludes).Add(match);
                    break;
            }
        }
        return new AppTargeting(includes, excludes);
    }

    private static (string Text, bool ThroughUser)? Via(string? groupId, DeviceMembership membership)
    {
        if (groupId is null) return null;
        if (membership.DeviceGroups.TryGetValue(groupId, out var deviceGroup)) return ($"{deviceGroup} (device group)", false);
        if (membership.UserGroups.TryGetValue(groupId, out var userGroup)) return ($"{userGroup} (user group)", true);
        return null;
    }
}
