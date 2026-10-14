using CommunityToolkit.Mvvm.ComponentModel;

namespace FieldKit.Core.ViewModels;

/// <summary>One scanned box in a pre-stage run (ports <c>PreStageResult</c>). Kept in memory only.</summary>
public sealed partial class PreStageResult : ObservableObject
{
    public PreStageResult(string serial)
    {
        Serial = serial;
        Status = PreStageStatus.Working;
    }

    public string Serial { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModel))]
    public partial string Model { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorking), nameof(Detail), nameof(HasDetail), nameof(IsWarning), nameof(IsFailed), nameof(IsDone), nameof(IsTagged), nameof(AutomationName))]
    public partial PreStageStatus Status { get; set; }

    public bool HasModel => Model.Length > 0;

    public bool IsWorking => Status is PreStageStatus.WorkingStatus;

    public bool IsFailed => Status is PreStageStatus.Failed;

    /// <summary>This run changed the tag (green); "already tagged" stays plain, as on iOS.</summary>
    public bool IsTagged => Status is PreStageStatus.Tagged;

    /// <summary>Tagged or already tagged.</summary>
    public bool IsDone => Status is PreStageStatus.Tagged or PreStageStatus.AlreadyTagged;

    /// <summary>Not in Autopilot, or failed: shown in the caution colour.</summary>
    public bool IsWarning => Status is PreStageStatus.NotInAutopilotStatus or PreStageStatus.Failed;

    public string Detail => Status switch
    {
        PreStageStatus.Tagged tagged => $"Tagged {tagged.Tag}",
        PreStageStatus.AlreadyTagged already => $"Already tagged {already.Tag}",
        PreStageStatus.NotInAutopilotStatus => "Not registered in Autopilot. Import the hardware hash first.",
        PreStageStatus.Failed failed => failed.Message,
        _ => "",
    };

    public bool HasDetail => Detail.Length > 0;

    public string AutomationName => DeviceText.Join(", ", Serial, Model, IsWorking ? "Tagging…" : Detail);
}
