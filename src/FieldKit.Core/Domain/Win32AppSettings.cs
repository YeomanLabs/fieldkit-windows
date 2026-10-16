namespace FieldKit.Core.Domain;

/// <summary>How a Win32 app installs and when it applies (Graph's <c>win32LobApp</c>): requirements, rules and return codes.</summary>
public sealed record Win32AppSettings
{
    /// <summary>Graph's <c>minimumSupportedWindowsRelease</c>, e.g. "1607" or "Windows11_23H2". Empty when unset.</summary>
    public string MinimumWindowsRelease { get; init; } = "";

    /// <summary>Allowed architectures in lower case ("x64", "arm64", "x86"); empty means any.</summary>
    public IReadOnlyList<string> Architectures { get; init; } = [];

    /// <summary>
    /// The list came from the older <c>applicableArchitectures</c>, which predates ARM64, so FieldKit
    /// doesn't judge an ARM64 device against it.
    /// </summary>
    public bool ArchitecturesAreLegacy { get; init; }

    public int? MinimumFreeDiskSpaceMb { get; init; }

    public int? MinimumMemoryMb { get; init; }

    public int? MinimumProcessors { get; init; }

    public int? MinimumCpuSpeedMhz { get; init; }

    public IReadOnlyList<AppRule> Rules { get; init; } = [];

    public IReadOnlyList<AppReturnCode> ReturnCodes { get; init; } = [];

    public string InstallCommand { get; init; } = "";

    public string UninstallCommand { get; init; } = "";

    /// <summary>The setup file inside the .intunewin package.</summary>
    public string SetupFile { get; init; } = "";

    /// <summary>"system" or "user": whose context the installer runs in.</summary>
    public string RunAsAccount { get; init; } = "";

    public bool InstallsAsUser => RunAsAccount.Equals("user", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<AppRule> DetectionRules => Rules.Where(rule => !rule.IsRequirement).ToList();

    public IReadOnlyList<AppRule> RequirementRules => Rules.Where(rule => rule.IsRequirement).ToList();
}
