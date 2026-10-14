using System.Text.Json.Serialization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// An Intune assignment: <c>{ "intent": "required", "target": { "@odata.type": "...", "groupId": "..." } }</c>.
/// The same shape is used by apps, profiles, Settings Catalog, compliance, scripts and remediations.
/// </summary>
internal sealed class AssignmentDto
{
    internal sealed class TargetDto
    {
        [JsonPropertyName("@odata.type")]
        public string? Type { get; init; }

        public string? GroupId { get; init; }
    }

    public string? Intent { get; init; }

    public TargetDto? Target { get; init; }

    public AssignmentEntry ToDomain()
    {
        var groupId = Target?.GroupId;
        var target = Target?.Type?.ToLowerInvariant() switch
        {
            "#microsoft.graph.alldevicesassignmenttarget" => AssignmentTarget.AllDevices,
            "#microsoft.graph.alllicensedusersassignmenttarget" => AssignmentTarget.AllUsers,
            "#microsoft.graph.groupassignmenttarget" when groupId is not null => AssignmentTarget.Group(groupId),
            "#microsoft.graph.exclusiongroupassignmenttarget" when groupId is not null => AssignmentTarget.ExcludedGroup(groupId),
            _ => AssignmentTarget.Other,
        };
        return new AssignmentEntry(target, Intent);
    }
}
