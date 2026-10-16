namespace FieldKit.Core.Domain;

/// <summary>An Intune app as "Why didn't it install?" needs it: type, assignments, related apps and, for Win32 apps, how it installs.</summary>
public sealed record AppDetails
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string Publisher { get; init; } = "";

    /// <summary>Graph's type without the namespace, e.g. "win32LobApp".</summary>
    public string TypeName { get; init; } = "";

    public IReadOnlyList<AppAssignment> Assignments { get; init; } = [];

    public IReadOnlyList<AppRelationship> Relationships { get; init; } = [];

    /// <summary>Set for Win32 apps only: other types have no rules or requirements FieldKit can read.</summary>
    public Win32AppSettings? Win32 { get; init; }

    public string TypeLabel => AppTypeNames.Label(TypeName);
}
