namespace FieldKit.Core.Domain;

/// <summary>
/// Works out which Intune assignments apply to a set of group memberships. Approximate by
/// design: assignment filters and Intune's rules for mixing user and device exclusions
/// aren't evaluated, so the UI says so.
/// </summary>
public static class AssignmentResolver
{
    /// <param name="Groups">Group ID to display name.</param>
    public sealed record Membership(IReadOnlyDictionary<string, string> Groups, bool IncludesAllDevices, bool IncludesAllUsers);

    /// <summary>Items with at least one matching include or exclude, excluded ones last.</summary>
    public static IReadOnlyList<ResolvedAssignment> Resolve(IEnumerable<AssignedItem> items, Membership membership)
    {
        var resolved = new List<ResolvedAssignment>();
        foreach (var item in items)
        {
            var included = new List<string>();
            var excluded = new List<string>();
            var intents = new List<string>();
            foreach (var entry in item.Assignments)
            {
                var target = entry.Target;
                switch (target.Kind)
                {
                    case AssignmentTargetKind.AllDevices when membership.IncludesAllDevices:
                        included.Add("All devices");
                        break;
                    case AssignmentTargetKind.AllUsers when membership.IncludesAllUsers:
                        included.Add("All users");
                        break;
                    case AssignmentTargetKind.Group:
                        if (!membership.Groups.TryGetValue(target.GroupId ?? "", out var name)) continue;
                        included.Add(name);
                        break;
                    case AssignmentTargetKind.ExcludedGroup:
                        if (membership.Groups.TryGetValue(target.GroupId ?? "", out var excludedName))
                        {
                            excluded.Add(excludedName);
                        }
                        continue;
                    default:
                        continue;
                }
                if (AssignedItem.IntentTitle(entry.Intent) is { } intent && !intents.Contains(intent))
                {
                    intents.Add(intent);
                }
            }
            if (included.Count == 0 && excluded.Count == 0) continue;
            resolved.Add(new ResolvedAssignment(item, intents, Unique(included), Unique(excluded)));
        }
        return resolved
            .OrderBy(assignment => assignment.IsExcluded)
            .ThenBy(assignment => assignment.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Items that include or exclude one group, for the group page.</summary>
    public static IReadOnlyList<ResolvedAssignment> Targeting(string groupId, string groupName, IEnumerable<AssignedItem> items) =>
        Resolve(items, new Membership(
            new Dictionary<string, string> { [groupId] = groupName },
            IncludesAllDevices: false,
            IncludesAllUsers: false));

    private static List<string> Unique(List<string> names) => names.Distinct().ToList();
}
