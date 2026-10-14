namespace FieldKit.Core.ViewModels;

/// <summary>Where one pre-staged serial is (ports <c>PreStageResult.Status</c>).</summary>
public abstract record PreStageStatus
{
    private PreStageStatus()
    {
    }

    public static PreStageStatus Working { get; } = new WorkingStatus();

    public static PreStageStatus NotInAutopilot { get; } = new NotInAutopilotStatus();

    public sealed record WorkingStatus : PreStageStatus;

    public sealed record Tagged(string Tag) : PreStageStatus;

    public sealed record AlreadyTagged(string Tag) : PreStageStatus;

    public sealed record NotInAutopilotStatus : PreStageStatus;

    public sealed record Failed(string Message) : PreStageStatus;
}
