namespace FieldKit.Core.ViewModels;

/// <summary>Progress of the last device action or secret request (ports <c>DeviceActionsModel.Status</c>).</summary>
public abstract record ActionStatus
{
    private ActionStatus()
    {
    }

    public static ActionStatus Idle { get; } = new IdleStatus();

    public sealed record IdleStatus : ActionStatus;

    public sealed record Running(string Text) : ActionStatus;

    public sealed record Done(string Text) : ActionStatus;

    public sealed record Failed(string Text) : ActionStatus;

    public bool IsRunning => this is Running;
}
