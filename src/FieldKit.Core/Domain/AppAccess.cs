namespace FieldKit.Core.Domain;

/// <summary>
/// Whether FieldKit may connect to a real tenant. Demo mode is always free.
/// Model: a free 14-day trial, then a one-time unlock. Ported at M0 for the iOS logic and tests;
/// unused on Windows, which is free (the user's decision on 2026-10-12, SPEC-windows).
/// </summary>
public readonly record struct AppAccess
{
    public static readonly TimeSpan TrialLength = TimeSpan.FromDays(14);

    private AppAccess(AppAccessState state, DateTimeOffset? endsAt = null)
    {
        State = state;
        EndsAt = endsAt;
    }

    public AppAccessState State { get; }

    /// <summary>When the trial ends (<see cref="AppAccessState.Trial"/>) or ended (<see cref="AppAccessState.TrialEnded"/>).</summary>
    public DateTimeOffset? EndsAt { get; }

    public static AppAccess Unknown => new(AppAccessState.Unknown);

    public static AppAccess NotStarted => new(AppAccessState.NotStarted);

    public static AppAccess Unlocked => new(AppAccessState.Unlocked);

    public static AppAccess Trial(DateTimeOffset endsAt) => new(AppAccessState.Trial, endsAt);

    public static AppAccess TrialEnded(DateTimeOffset endedAt) => new(AppAccessState.TrialEnded, endedAt);

    /// <summary>From what the user owns: the unlock, and when they started the trial (if ever).</summary>
    public static AppAccess From(bool ownsUnlock, DateTimeOffset? trialStartedAt, DateTimeOffset? now = null)
    {
        if (ownsUnlock) return Unlocked;
        if (trialStartedAt is not { } started) return NotStarted;
        var endsAt = started + TrialLength;
        return (now ?? DateTimeOffset.Now) < endsAt ? Trial(endsAt) : TrialEnded(endsAt);
    }

    /// <summary>Connecting to a real tenant needs an active trial or the unlock.</summary>
    public bool AllowsTenant => State is AppAccessState.Trial or AppAccessState.Unlocked;

    /// <summary>Whole days left in the trial, rounded up so the last day shows as 1.</summary>
    public int? TrialDaysLeft(DateTimeOffset? now = null)
    {
        if (State != AppAccessState.Trial || EndsAt is not { } endsAt) return null;
        var days = (endsAt - (now ?? DateTimeOffset.Now)).TotalDays;
        return Math.Max(1, (int)Math.Ceiling(days));
    }
}
