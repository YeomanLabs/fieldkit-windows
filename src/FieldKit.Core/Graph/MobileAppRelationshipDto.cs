using System.Text.Json.Serialization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// One of an app's <c>relationships</c> (beta only): a <c>mobileAppDependency</c> or a
/// <c>mobileAppSupersedence</c>. Shape from Microsoft's reference; no test tenant has one yet.
/// </summary>
internal sealed class MobileAppRelationshipDto
{
    [JsonPropertyName("@odata.type")]
    public string? Type { get; init; }

    public string? TargetId { get; init; }

    public string? TargetDisplayName { get; init; }

    /// <summary>"child" or "parent": where the other app sits in the relationship.</summary>
    public string? TargetType { get; init; }

    /// <summary>"detect" or "autoInstall".</summary>
    public string? DependencyType { get; init; }

    /// <summary>"update" or "replace".</summary>
    public string? SupersedenceType { get; init; }

    public AppRelationship ToDomain() =>
        new()
        {
            TargetId = TargetId ?? "",
            TargetName = TargetDisplayName ?? "",
            Kind = Type?.Replace("#microsoft.graph.", "", StringComparison.Ordinal).ToLowerInvariant() switch
            {
                "mobileappdependency" => AppRelationshipKind.Dependency,
                "mobileappsupersedence" => AppRelationshipKind.Supersedence,
                _ => AppRelationshipKind.Other,
            },
            TargetIsChild = string.Equals(TargetType, "child", StringComparison.OrdinalIgnoreCase),
            AutoInstall = string.Equals(DependencyType, "autoInstall", StringComparison.OrdinalIgnoreCase),
            Replaces = string.Equals(SupersedenceType, "replace", StringComparison.OrdinalIgnoreCase),
        };
}
