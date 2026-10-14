namespace FieldKit.Core.Tests;

public sealed class AppAccessTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    [Fact]
    public void UnlockWinsOverEverything()
    {
        Assert.Equal(AppAccess.Unlocked, AppAccess.From(ownsUnlock: true, trialStartedAt: null, now: Start));
        Assert.Equal(AppAccess.Unlocked, AppAccess.From(ownsUnlock: true, trialStartedAt: Start - 90 * Day, now: Start));
    }

    [Fact]
    public void NoTrialMeansNotStarted()
    {
        var access = AppAccess.From(ownsUnlock: false, trialStartedAt: null, now: Start);
        Assert.Equal(AppAccess.NotStarted, access);
        Assert.False(access.AllowsTenant);
    }

    [Fact]
    public void TrialRunsFourteenDays()
    {
        var ends = Start + 14 * Day;
        var day13 = AppAccess.From(ownsUnlock: false, trialStartedAt: Start, now: ends - TimeSpan.FromSeconds(1));
        Assert.Equal(AppAccess.Trial(ends), day13);
        Assert.True(day13.AllowsTenant);

        var over = AppAccess.From(ownsUnlock: false, trialStartedAt: Start, now: ends);
        Assert.Equal(AppAccess.TrialEnded(ends), over);
        Assert.False(over.AllowsTenant);
    }

    [Fact]
    public void DaysLeftRoundsUp()
    {
        var access = AppAccess.From(ownsUnlock: false, trialStartedAt: Start, now: Start);
        Assert.Equal(14, access.TrialDaysLeft(Start));
        Assert.Equal(13, access.TrialDaysLeft(Start + Day + TimeSpan.FromSeconds(60)));
        Assert.Equal(1, access.TrialDaysLeft(Start + 14 * Day - TimeSpan.FromSeconds(60)));
        Assert.Null(AppAccess.Unlocked.TrialDaysLeft(Start));
    }

    [Fact]
    public void OnlyTrialAndUnlockAllowTenant()
    {
        Assert.False(AppAccess.Unknown.AllowsTenant);
        Assert.True(AppAccess.Unlocked.AllowsTenant);
    }
}
