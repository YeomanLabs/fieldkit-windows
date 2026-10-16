using System.Text.Json.Serialization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// One Intune app (beta), read in two parts: its type with <c>$expand=assignments,relationships</c>,
/// then, for Win32 apps, the Win32 fields by their type-qualified names. Graph won't select a
/// subtype's field without the qualifier, and the qualifier fails on any other type.
/// </summary>
internal sealed class MobileAppDto
{
    private const string Win32 = "microsoft.graph.win32LobApp";

    public const string BaseSelect = "id,displayName,publisher";

    public static readonly string Win32Select = "id," + string.Join(",", new[]
    {
        "displayVersion", "rules", "allowedArchitectures", "applicableArchitectures", "minimumSupportedWindowsRelease",
        "minimumFreeDiskSpaceInMB", "minimumMemoryInMB", "minimumNumberOfProcessors", "minimumCpuSpeedInMHz",
        "returnCodes", "installExperience", "installCommandLine", "uninstallCommandLine", "setupFilePath",
    }.Select(field => $"{Win32}/{field}"));

    public required string Id { get; init; }

    [JsonPropertyName("@odata.type")]
    public string? Type { get; init; }

    public string? DisplayName { get; init; }

    public string? Publisher { get; init; }

    public IReadOnlyList<AssignmentDto>? Assignments { get; init; }

    public IReadOnlyList<MobileAppRelationshipDto>? Relationships { get; init; }

    // Win32 apps only.

    public IReadOnlyList<Win32LobAppRuleDto>? Rules { get; init; }

    /// <summary>"x64,arm64"; null or "none" when unset.</summary>
    public string? AllowedArchitectures { get; init; }

    /// <summary>The older setting: "x86,x64", or "none" when <see cref="AllowedArchitectures"/> is used.</summary>
    public string? ApplicableArchitectures { get; init; }

    public string? MinimumSupportedWindowsRelease { get; init; }

    public int? MinimumFreeDiskSpaceInMB { get; init; }

    public int? MinimumMemoryInMB { get; init; }

    public int? MinimumNumberOfProcessors { get; init; }

    public int? MinimumCpuSpeedInMHz { get; init; }

    public IReadOnlyList<Win32LobAppReturnCodeDto>? ReturnCodes { get; init; }

    public Win32LobAppInstallExperienceDto? InstallExperience { get; init; }

    public string? InstallCommandLine { get; init; }

    public string? UninstallCommandLine { get; init; }

    public string? SetupFilePath { get; init; }

    public string TypeName => Type?.Replace("#microsoft.graph.", "", StringComparison.Ordinal) ?? "";

    public bool IsWin32 => TypeName == AppTypeNames.Win32;

    /// <param name="win32">The second read, for Win32 apps.</param>
    public AppDetails ToDomain(MobileAppDto? win32) =>
        new()
        {
            Id = Id,
            Name = DisplayName ?? "Unnamed app",
            Publisher = Publisher ?? "",
            TypeName = TypeName,
            Assignments = (Assignments ?? []).Select(assignment => assignment.ToAppAssignment()).ToList(),
            Relationships = (Relationships ?? []).Select(relationship => relationship.ToDomain()).ToList(),
            Win32 = win32?.ToWin32(),
        };

    private Win32AppSettings ToWin32()
    {
        var allowed = Architectures(AllowedArchitectures);
        return new Win32AppSettings
        {
            MinimumWindowsRelease = MinimumSupportedWindowsRelease ?? "",
            Architectures = allowed.Count > 0 ? allowed : Architectures(ApplicableArchitectures),
            ArchitecturesAreLegacy = allowed.Count == 0,
            MinimumFreeDiskSpaceMb = MinimumFreeDiskSpaceInMB,
            MinimumMemoryMb = MinimumMemoryInMB,
            MinimumProcessors = MinimumNumberOfProcessors,
            MinimumCpuSpeedMhz = MinimumCpuSpeedInMHz,
            Rules = (Rules ?? []).Select(rule => rule.ToDomain()).ToList(),
            ReturnCodes = (ReturnCodes ?? []).Where(code => code.ReturnCode is not null)
                .Select(code => new AppReturnCode(code.ReturnCode!.Value, code.Type ?? "")).ToList(),
            InstallCommand = InstallCommandLine ?? "",
            UninstallCommand = UninstallCommandLine ?? "",
            SetupFile = SetupFilePath ?? "",
            RunAsAccount = InstallExperience?.RunAsAccount ?? "",
        };
    }

    /// <summary>"x64,arm64" → ["x64", "arm64"]; "none" and "neutral" mean no restriction.</summary>
    private static List<string> Architectures(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(architecture => architecture.ToLowerInvariant())
            .Where(architecture => architecture is not ("none" or "neutral"))
            .ToList();
}
