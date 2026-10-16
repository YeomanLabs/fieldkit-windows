using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>A dependency or supersedence, with how the other app is doing on this device.</summary>
public sealed record RelatedAppItem(AppRelationship Relationship, AppInstallStatus? OnDevice)
{
    public string Description => Relationship.Description;

    public string StateText => OnDevice is { } app ? $"{app.Outcome.DisplayName(app.Intent)} on this device" : "Not assigned to this device";

    public InfoTone Tone => OnDevice?.Outcome switch
    {
        AppInstallOutcome.Failed or AppInstallOutcome.NotApplicable => InfoTone.Critical,
        AppInstallOutcome.Pending or AppInstallOutcome.NoStatus or AppInstallOutcome.Missing => InfoTone.Caution,
        AppInstallOutcome.Installed => InfoTone.Good,
        _ => InfoTone.Normal,
    };

    public string AutomationName => $"{Description}: {StateText}";
}
