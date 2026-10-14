using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Persistence;

namespace FieldKit.Core.ViewModels;

/// <summary>One scanned device in a site walk (ports <c>ScanRowView</c>); updates itself as its lookup finishes.</summary>
public sealed partial class ScanItem : ObservableObject
{
    public ScanItem(ScanRecord record)
    {
        Record = record;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HasName), nameof(IsPending), nameof(IsFailed), nameof(IsDone), nameof(HasFlags),
        nameof(NoFlags), nameof(FlagCountText), nameof(FailureText), nameof(SeverityTitle), nameof(IsCritical), nameof(AutomationName))]
    public partial ScanRecord Record { get; set; }

    public string Serial => Record.Serial;

    /// <summary>The device's name, or the serial until (or unless) one is found.</summary>
    public string Title => Record.DisplayName ?? Record.Serial;

    /// <summary>When there's a name, the serial shows under it.</summary>
    public bool HasName => Record.DisplayName is not null;

    public bool IsPending => Record.Status == ScanStatus.Pending;

    public bool IsFailed => Record.Status == ScanStatus.Failed;

    public bool IsDone => Record.Status == ScanStatus.Done;

    public bool HasFlags => IsDone && Record.FlagValues.Count > 0;

    public bool NoFlags => IsDone && Record.FlagValues.Count == 0;

    public string FlagCountText => Record.FlagValues.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string FailureText => IsFailed ? Record.FailureMessage ?? "" : "";

    /// <summary>"Critical", "Warning" or "Info": the worst flag, for colour.</summary>
    public string SeverityTitle => Record.Severity?.Title() ?? "";

    public bool IsCritical => Record.Severity == FlagSeverity.Critical;

    public string AutomationName => Record.Status switch
    {
        ScanStatus.Pending => $"{Title}, looking up",
        ScanStatus.Failed => $"{Title}, lookup failed: {Record.FailureMessage}",
        _ when HasFlags => $"{Title}, {Record.FlagValues.Count} flags, worst {SeverityTitle}",
        _ => $"{Title}, no flags",
    };
}
