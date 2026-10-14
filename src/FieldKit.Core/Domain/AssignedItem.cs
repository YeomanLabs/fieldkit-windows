namespace FieldKit.Core.Domain;

/// <summary>An Intune app, profile, policy or script, with its assignments.</summary>
public sealed record AssignedItem
{
    // Always set when built from Graph; defaults only so XAML can create the type (list items).

    public string Id { get; init; } = "";

    public AssignmentKind Kind { get; init; }

    public string Name { get; init; } = "";

    /// <summary>A short type or platform label, e.g. "Windows app (Win32)".</summary>
    public string Detail { get; init; } = "";

    public IReadOnlyList<AssignmentEntry> Assignments { get; init; } = [];

    /// <summary>Graph's type without the namespace, e.g. "win32LobApp". Empty when unknown.</summary>
    public string TypeName { get; init; } = "";

    /// <summary>The platform an app targets; null for policies and cross-platform apps.</summary>
    public AppPlatform? Platform => Kind == AssignmentKind.App ? AppPlatformParser.FromAppType(TypeName) : null;

    public static string? IntentTitle(string? intent) =>
        intent?.ToLowerInvariant() switch
        {
            "required" => "Required",
            "available" => "Available",
            "uninstall" => "Uninstall",
            "availablewithoutenrollment" => "Available without enrollment",
            null or "" => null,
            _ => intent,
        };
}
