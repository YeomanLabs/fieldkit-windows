namespace FieldKit.Core.Domain;

/// <summary>A Conditional Access policy Entra evaluated for a sign-in, and what it decided.</summary>
public sealed record SignInPolicy(string Id, string Name, SignInPolicyResult Result, IReadOnlyList<string> GrantControls, IReadOnlyList<string> SessionControls)
{
    /// <summary>"Require compliant device · Require multifactor authentication", from Graph's control names.</summary>
    public string ControlsText => string.Join(" · ", GrantControls.Concat(SessionControls).Select(SignInControls.Describe));

    public bool Failed => Result == SignInPolicyResult.Failure;
}
