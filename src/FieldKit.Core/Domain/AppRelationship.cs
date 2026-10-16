namespace FieldKit.Core.Domain;

/// <summary>
/// A dependency or supersedence between this app and another (Graph's <c>mobileAppRelationship</c>,
/// beta only). The parent depends on, or supersedes, the child.
/// </summary>
public sealed record AppRelationship
{
    public string TargetId { get; init; } = "";

    public string TargetName { get; init; } = "";

    public AppRelationshipKind Kind { get; init; }

    /// <summary>The other app is the child: this app depends on it, or supersedes it.</summary>
    public bool TargetIsChild { get; init; }

    /// <summary>Dependencies: Intune installs the child first ("autoInstall"), rather than only checking for it ("detect").</summary>
    public bool AutoInstall { get; init; }

    /// <summary>Supersedence: the old app is uninstalled first ("replace"), rather than updated in place ("update").</summary>
    public bool Replaces { get; init; }

    /// <summary>This app needs the other one on the device first.</summary>
    public bool IsDependencyOfThisApp => Kind == AppRelationshipKind.Dependency && TargetIsChild;

    public string Description =>
        (Kind, TargetIsChild) switch
        {
            (AppRelationshipKind.Dependency, true) => AutoInstall
                ? $"Depends on {TargetName}, which Intune installs first"
                : $"Depends on {TargetName}, which must already be installed",
            (AppRelationshipKind.Dependency, false) => $"{TargetName} depends on this app",
            (AppRelationshipKind.Supersedence, true) => Replaces
                ? $"Replaces {TargetName} (uninstalls it first)"
                : $"Updates {TargetName}",
            (AppRelationshipKind.Supersedence, false) => $"Superseded by {TargetName}",
            _ => $"Related to {TargetName}",
        };
}
