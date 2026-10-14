using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// "Why was I blocked?" for one sign-in (Windows only; the iOS roadmap's phase 4): the verdict,
/// what to check, the Conditional Access policies and the device Entra saw.
/// </summary>
public sealed partial class SignInDetailViewModel : ObservableObject
{
    public const string PoliciesHiddenText =
        "FieldKit can't see the Conditional Access policies for this sign-in. That needs the Policy.Read.ConditionalAccess permission and a role such as Global Reader, Security Reader or Conditional Access Administrator.";

    private readonly IReadOnlyList<SignInPolicy> _policies;

    public SignInDetailViewModel(SignIn signIn, TimeProvider? time = null)
    {
        SignIn = signIn;
        Diagnosis = SignInDecoder.Diagnose(signIn);
        When = DateText.RelativeAndAbsolute(signIn.CreatedAt, (time ?? TimeProvider.System).GetUtcNow());
        // Blocked first, then what would block, what passed, and the rest.
        _policies = (signIn.Policies ?? []).OrderBy(policy => policy.Result switch
        {
            SignInPolicyResult.Failure => 0,
            SignInPolicyResult.ReportOnlyFailure => 1,
            SignInPolicyResult.ReportOnlyInterrupted => 2,
            SignInPolicyResult.Success => 3,
            SignInPolicyResult.ReportOnlySuccess => 4,
            _ => 5,
        }).ThenBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public SignIn SignIn { get; }

    public SignInDiagnosis Diagnosis { get; }

    public string When { get; }

    public string Title => Diagnosis.Title;

    public SignInVerdict Verdict => Diagnosis.Verdict;

    public string Subtitle => DeviceText.Join(" · ", SignIn.UserDisplayName, AppText, When);

    public string Explanation => Diagnosis.Explanation;

    public IReadOnlyList<string> WhatToCheck => Diagnosis.WhatToCheck;

    public bool HasChecks => WhatToCheck.Count > 0;

    public bool PoliciesVisible => SignIn.Policies is not null;

    /// <summary><see cref="PoliciesHiddenText"/> for the page (XAML binds to instance members only).</summary>
    public string PoliciesHiddenMessage => PoliciesHiddenText;

    /// <summary>Also show the policies that didn't apply or are turned off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PolicyList), nameof(ShowAllText), nameof(HasPolicies))]
    public partial bool ShowsAllPolicies { get; set; }

    public IReadOnlyList<SignInPolicyItem> PolicyList =>
        _policies.Where(policy => ShowsAllPolicies || policy.Result.Applied()).Select(SignInPolicyItem.From).ToList();

    public bool HasPolicies => PolicyList.Count > 0;

    public bool HasPoliciesThatDidNotApply => _policies.Any(policy => !policy.Result.Applied());

    public string ShowAllText => ShowsAllPolicies
        ? "Show only the policies that applied"
        : $"Show all {_policies.Count} policies, including ones that didn't apply";

    /// <summary>When the list is empty: nothing applied (the toggle can show the rest).</summary>
    public string NoPoliciesText => "No Conditional Access policy applied to this sign-in.";

    public string AppText => SignIn.AppName.Length > 0 ? SignIn.AppName : SignIn.ResourceName;

    public IReadOnlyList<InfoRow> DeviceRows
    {
        get
        {
            var device = SignIn.Device;
            var rows = new List<InfoRow>
            {
                new("Device", device.DisplayName.Length > 0 ? device.DisplayName : device.HasIdentity ? device.DeviceId : "No device identity",
                    device.HasIdentity ? InfoTone.Normal : InfoTone.Caution),
            };
            if (device.OperatingSystem.Length > 0) rows.Add(new("Operating system", device.OperatingSystem));
            if (device.Browser.Length > 0) rows.Add(new("Browser", device.Browser));
            if (device.TrustType.Length > 0) rows.Add(new("Join type", device.TrustType));
            if (device.IsCompliant is { } compliant) rows.Add(new("Compliant", compliant ? "Yes" : "No", compliant ? InfoTone.Good : InfoTone.Critical));
            if (device.IsManaged is { } managed) rows.Add(new("Managed", managed ? "Yes" : "No"));
            return rows;
        }
    }

    /// <summary>The device can be opened in FieldKit by its Entra device ID.</summary>
    public bool CanOpenDevice => SignIn.Device.HasIdentity;

    public string OpenDeviceText => $"Open {(SignIn.Device.DisplayName.Length > 0 ? SignIn.Device.DisplayName : "the device")} in FieldKit";

    public IReadOnlyList<InfoRow> DetailRows
    {
        get
        {
            var rows = new List<InfoRow> { new("App", AppText.Length > 0 ? AppText : "—") };
            if (SignIn.ResourceName.Length > 0 && SignIn.ResourceName != SignIn.AppName) rows.Add(new("Resource", SignIn.ResourceName));
            if (SignIn.ClientApp.Length > 0) rows.Add(new("Client app", SignIn.ClientApp));
            if (SignIn.IpAddress.Length > 0) rows.Add(new("IP address", SignIn.IpAddress));
            if (SignIn.Location.Length > 0) rows.Add(new("Location", SignIn.Location));
            rows.Add(new("When", When));
            if (SignIn.ErrorCode != 0)
            {
                rows.Add(new("Error code", $"AADSTS{SignIn.ErrorCode}"));
                if (SignIn.FailureReason.Length > 0) rows.Add(new("Entra's reason", SignIn.FailureReason));
                if (SignIn.AdditionalDetails.Length > 0) rows.Add(new("Details", SignIn.AdditionalDetails));
            }
            return rows;
        }
    }

    public string RequestId => SignIn.Id;

    public string CorrelationId => SignIn.CorrelationId;

    public bool HasCorrelationId => CorrelationId.Length > 0;
}
