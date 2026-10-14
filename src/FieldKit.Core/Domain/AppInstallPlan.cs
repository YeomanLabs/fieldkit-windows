namespace FieldKit.Core.Domain;

/// <summary>
/// How an app could be installed on a device through group membership. Intune installs an
/// app when it's assigned as Required to a group the device or its user is in, so
/// "install" means joining one of those groups.
/// </summary>
public static class AppInstallPlan
{
    public enum StatusKind
    {
        NotRequired,
        /// <summary>Already required through <see cref="Status.Via"/> (groups, or All devices / All users).</summary>
        Required,
        /// <summary>Required somewhere, but an exclusion (<see cref="Status.Via"/>) covers this device or user.</summary>
        Excluded,
    }

    public sealed record Status(StatusKind Kind, IReadOnlyList<string> Via)
    {
        public static Status NotRequired { get; } = new(StatusKind.NotRequired, []);
    }

    /// <summary>Whether the app is already required for a device with these memberships.</summary>
    public static Status StatusOf(
        AssignedItem app,
        IReadOnlyDictionary<string, string> deviceGroups,
        IReadOnlyDictionary<string, string> userGroups,
        bool hasUser)
    {
        var groups = new Dictionary<string, string>(deviceGroups);
        foreach (var (id, name) in userGroups)
        {
            groups.TryAdd(id, name);
        }
        var membership = new AssignmentResolver.Membership(groups, IncludesAllDevices: true, IncludesAllUsers: hasUser);
        var resolved = AssignmentResolver.Resolve([RequiredOnly(app)], membership).FirstOrDefault();
        if (resolved is null) return Status.NotRequired;
        if (resolved.IsExcluded) return new Status(StatusKind.Excluded, resolved.ExcludedVia);
        return resolved.IncludedVia.Count == 0 ? Status.NotRequired : new Status(StatusKind.Required, resolved.IncludedVia);
    }

    /// <summary>Group IDs the app is assigned to as Required: the groups joining would install it.</summary>
    public static IReadOnlyList<string> RequiredGroupIds(AssignedItem app)
    {
        var seen = new HashSet<string>();
        var ids = new List<string>();
        foreach (var entry in app.Assignments)
        {
            if (IsRequired(entry) && entry.Target.Kind == AssignmentTargetKind.Group
                && entry.Target.GroupId is { } id && seen.Add(id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>Whether the app is required for everyone, in which case no group change is needed.</summary>
    public static (bool Devices, bool Users) RequiredForAll(AssignedItem app)
    {
        var required = app.Assignments.Where(IsRequired).ToList();
        return (
            required.Any(entry => entry.Target == AssignmentTarget.AllDevices),
            required.Any(entry => entry.Target == AssignmentTarget.AllUsers));
    }

    private static AssignedItem RequiredOnly(AssignedItem app) =>
        // Keep exclusions: they apply whatever the intent.
        app with { Assignments = app.Assignments.Where(entry => IsRequired(entry) || IsExclusion(entry)).ToList() };

    private static bool IsRequired(AssignmentEntry entry) =>
        string.Equals(entry.Intent, "required", StringComparison.OrdinalIgnoreCase);

    private static bool IsExclusion(AssignmentEntry entry) =>
        entry.Target.Kind == AssignmentTargetKind.ExcludedGroup;
}
