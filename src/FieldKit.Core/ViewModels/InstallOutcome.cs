namespace FieldKit.Core.ViewModels;

/// <summary>The last add, remove or sync on the install dialog (ports <c>InstallAppModel.Outcome</c>).</summary>
public abstract record InstallOutcome
{
    private InstallOutcome()
    {
    }

    public static InstallOutcome Idle { get; } = new IdleOutcome();

    public static InstallOutcome Working { get; } = new WorkingOutcome();

    public sealed record IdleOutcome : InstallOutcome;

    public sealed record WorkingOutcome : InstallOutcome;

    /// <summary>Success: added, removed or synced.</summary>
    public sealed record Added(string Text) : InstallOutcome;

    public sealed record Failed(string Text) : InstallOutcome;
}
