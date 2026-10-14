using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>A Conditional Access policy on the sign-in page: its name, what it decided, and what it asks for.</summary>
public sealed record SignInPolicyItem(string Id, string Name, string ResultText, InfoTone Tone, string ControlsText)
{
    public bool HasControls => ControlsText.Length > 0;

    public string AutomationName => HasControls ? $"{Name}: {ResultText}. {ControlsText}" : $"{Name}: {ResultText}";

    public static SignInPolicyItem From(SignInPolicy policy) =>
        new(policy.Id, policy.Name, policy.Result.DisplayName(),
            policy.Result switch
            {
                SignInPolicyResult.Failure => InfoTone.Critical,
                SignInPolicyResult.ReportOnlyFailure or SignInPolicyResult.ReportOnlyInterrupted => InfoTone.Caution,
                SignInPolicyResult.Success => InfoTone.Good,
                _ => InfoTone.Normal,
            },
            policy.ControlsText);
}
