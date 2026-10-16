using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>One assignment that reaches (or excludes) the device: "Required · CTS Laptops (device group)".</summary>
public sealed record AppTargetItem(AppTargetMatch Match)
{
    public string Intent => Match.IntentTitle;

    public string Via => Match.Via;

    public string FilterText => Match.FilterText;

    public bool HasFilter => FilterText.Length > 0;

    public InfoTone Tone => Match.IsExclusion ? InfoTone.Caution : Match.IsRequired ? InfoTone.Good : InfoTone.Normal;

    public string AutomationName => DeviceText.Join(". ", $"{Intent} through {Via}", FilterText);
}
