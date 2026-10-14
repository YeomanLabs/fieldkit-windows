namespace FieldKit.Core.Domain;

/// <summary>The cases of <see cref="AppAccess"/>.</summary>
public enum AppAccessState
{
    /// <summary>Entitlements haven't been read yet.</summary>
    Unknown,
    NotStarted,
    Trial,
    TrialEnded,
    Unlocked,
}
