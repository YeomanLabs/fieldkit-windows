using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>One configuration profile or policy on the install status tab.</summary>
public sealed record PolicyStatusItem(PolicyDeploymentStatus Policy, string When)
{
    public static PolicyStatusItem From(PolicyDeploymentStatus policy, DateTimeOffset now) =>
        new(policy, policy.LastReported is { } at ? DateText.Relative(at, now) : "");

    public string Name => Policy.Name;

    /// <summary>"Settings Catalog · reported 2 days ago".</summary>
    public string Subtitle => DeviceText.Join(" · ", Policy.TypeName, When.Length > 0 ? $"reported {When}" : "");

    public string StateText => Policy.State.DisplayName();

    public InfoTone Tone => Policy.State switch
    {
        PolicyDeploymentState.Error => InfoTone.Critical,
        PolicyDeploymentState.Conflict or PolicyDeploymentState.NotReported => InfoTone.Caution,
        PolicyDeploymentState.Succeeded => InfoTone.Good,
        _ => InfoTone.Normal,
    };

    public string AutomationName => DeviceText.Join(". ", $"{Name}: {StateText}", Subtitle);
}
