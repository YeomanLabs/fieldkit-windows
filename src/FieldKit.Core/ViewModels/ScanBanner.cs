namespace FieldKit.Core.ViewModels;

/// <summary>
/// Feedback after each scan in pre-staging and site walks (ports <c>ScanBanner</c>). Windows
/// shows it inline beside the serial box, in place of iOS's haptic.
/// </summary>
public sealed record ScanBanner(string Text, bool IsWarning)
{
    /// <summary>Makes a repeated message a new value, so it's announced again.</summary>
    public Guid Id { get; } = Guid.NewGuid();
}
