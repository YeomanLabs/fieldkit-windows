using System.Text.Json.Serialization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// A directory object from <c>/groups</c>, <c>/memberOf</c> or <c>/members</c>. Only some fields
/// apply to each type; <c>@odata.type</c> says which it is.
/// </summary>
internal sealed class DirectoryObjectDto
{
    public const string GroupSelect = "id,displayName,description,groupTypes,membershipRule,securityEnabled";

    public required string Id { get; init; }

    [JsonPropertyName("@odata.type")]
    public string? Type { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string>? GroupTypes { get; init; }

    public string? MembershipRule { get; init; }

    public bool? SecurityEnabled { get; init; }

    public string? UserPrincipalName { get; init; }

    public string? DeviceId { get; init; }

    public string? OperatingSystem { get; init; }

    public bool IsGroup =>
        string.Equals(Type, "#microsoft.graph.group", StringComparison.OrdinalIgnoreCase)
        || (Type is null && GroupTypes is not null);

    public DirectoryGroup ToGroup() =>
        new()
        {
            Id = Id,
            DisplayName = DisplayName ?? "Unnamed group",
            Description = Description ?? "",
            GroupTypes = GroupTypes ?? [],
            MembershipRule = MembershipRule.NullIfEmpty(),
            SecurityEnabled = SecurityEnabled ?? false,
        };

    public GroupMember ToMember()
    {
        GroupMemberKind kind = Type?.ToLowerInvariant() switch
        {
            "#microsoft.graph.user" => new GroupMemberKind.User(UserPrincipalName ?? ""),
            "#microsoft.graph.device" => new GroupMemberKind.Device(DeviceId, OperatingSystem ?? ""),
            "#microsoft.graph.group" => new GroupMemberKind.Group(),
            _ => new GroupMemberKind.Other(),
        };
        return new GroupMember(Id, DisplayName ?? UserPrincipalName ?? "Unnamed", kind);
    }
}
