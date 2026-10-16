namespace FieldKit.Core.Graph;

/// <summary>A Win32 app's <c>installExperience</c>; only whose context it runs in is read.</summary>
internal sealed class Win32LobAppInstallExperienceDto
{
    /// <summary>"system" or "user".</summary>
    public string? RunAsAccount { get; init; }
}
