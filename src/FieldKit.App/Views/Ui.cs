using FieldKit.App.Controls;
using FieldKit.Core.Domain;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;

namespace FieldKit.App.Views;

/// <summary>
/// Small functions for x:Bind, so pages need no converters.
/// <para>
/// x:Bind re-runs a function only when an argument changes to a non-null value, so a binding
/// whose only argument becomes null keeps its old result. Every function here therefore takes
/// at least one argument that is never null where it's used (a bool, a struct, an item's ID or
/// name, or an object that's set whenever its card is visible). Visibility that depends on
/// something optional binds to a bool on the view model instead.
/// </para>
/// </summary>
public static class Ui
{
    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>For strings that are empty rather than null when unset.</summary>
    public static Visibility VisibleIfText(string value) => Visible(value.Length > 0);

    public static bool Not(bool value) => !value;

    public static string YesNo(bool value) => value ? "Yes" : "No";

    public static Tone ComplianceTone(ComplianceState state) =>
        state.Kind == ComplianceStateKind.Compliant ? Tone.Good : Tone.Caution;

    public static Tone PolicyTone(PolicyComplianceState state) =>
        state.Kind switch
        {
            PolicyComplianceStateKind.Compliant or PolicyComplianceStateKind.Remediated => Tone.Good,
            PolicyComplianceStateKind.Noncompliant or PolicyComplianceStateKind.Error or PolicyComplianceStateKind.Conflict => Tone.Caution,
            _ => Tone.Normal,
        };

    public static Tone EnabledTone(bool enabled) => enabled ? Tone.Normal : Tone.Critical;

    /// <summary>Granted is green, missing is amber, an optional one that isn't granted is plain.</summary>
    public static Tone PermissionTone(bool granted, bool missing) => granted ? Tone.Good : missing ? Tone.Caution : Tone.Normal;

    /// <summary>Signed in is green, interrupted or failed amber, blocked red.</summary>
    public static Tone VerdictTone(FieldKit.Core.Domain.SignInVerdict verdict) =>
        verdict switch
        {
            FieldKit.Core.Domain.SignInVerdict.Success => Tone.Good,
            FieldKit.Core.Domain.SignInVerdict.Blocked => Tone.Critical,
            _ => Tone.Caution,
        };

    /// <summary>Newly tagged is green, a problem is amber, anything else is plain.</summary>
    public static Tone PreStageTone(bool isWarning, bool isTagged) => isWarning ? Tone.Caution : isTagged ? Tone.Good : Tone.Normal;

    /// <summary>An app's install status: required is green, excluded amber (ports <c>InstallAppRow</c>'s badges).</summary>
    public static Tone InstallTone(bool isRequired, bool isExcluded) => isExcluded ? Tone.Caution : isRequired ? Tone.Good : Tone.Normal;

    public static Tone InstallStatusTone(FieldKit.Core.Domain.AppInstallPlan.StatusKind kind) =>
        kind switch
        {
            FieldKit.Core.Domain.AppInstallPlan.StatusKind.Required => Tone.Good,
            FieldKit.Core.Domain.AppInstallPlan.StatusKind.Excluded => Tone.Caution,
            _ => Tone.Normal,
        };

    /// <summary>A scan's flag count, coloured by its worst flag: critical red, warning amber, info plain.</summary>
    public static Tone SeverityTone(string severityTitle) =>
        severityTitle switch
        {
            "Critical" => Tone.Critical,
            "Warning" => Tone.Caution,
            _ => Tone.Normal,
        };

    public static Tone InfoRowTone(InfoTone tone) =>
        tone switch
        {
            InfoTone.Good => Tone.Good,
            InfoTone.Caution => Tone.Caution,
            InfoTone.Critical => Tone.Critical,
            _ => Tone.Normal,
        };

    /// <summary>"Publisher · size" under a detected app; anchored on its ID, which is never null.</summary>
    public static string DetectedAppDetail(string id, string publisher, long? sizeBytes) =>
        DeviceInventoryViewModel.AppDetail(new DetectedApp(id, "", "", publisher, sizeBytes));

    /// <summary>Scan feedback: amber for a warning, green otherwise (iOS: orange or green).</summary>
    public static Tone BannerTone(bool isWarning) => isWarning ? Tone.Caution : Tone.Good;

    /// <summary>A device action: in progress (informational), sent (success) or failed (warning).</summary>
    public static Microsoft.UI.Xaml.Controls.InfoBarSeverity ActionSeverity(bool done, bool failed) =>
        failed ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning
        : done ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success
        : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;

    public static Microsoft.UI.Xaml.Controls.InfoBarSeverity NoticeSeverity(bool isError) =>
        isError ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success;

    /// <summary>"Checking…" until the first flag arrives or every section is done.</summary>
    public static Visibility Checking(bool hasFlags, bool loading) => Visible(!hasFlags && loading);

    public static Visibility NoProblems(bool hasFlags, bool loading, bool failed) => Visible(!hasFlags && !loading && !failed);

    /// <summary>A section the flags depend on failed, so they may be missing some.</summary>
    public static Visibility Incomplete(bool loading, bool failed) => Visible(!loading && failed);

    // The selected Intune record and the Entra device are set whenever their cards show.

    public static string PrimaryUser(IntuneDevice? device) => device is null ? "" : DeviceText.PrimaryUser(device);

    public static string LastSync(IntuneDevice? device) => device is null ? "" : DateText.RelativeAndAbsolute(device.LastSyncDateTime);

    public static string Enrolled(IntuneDevice? device) => device is null ? "" : DateText.Absolute(device.EnrolledDateTime);

    public static string OperatingSystem(IntuneDevice? device) => device is null ? "" : DeviceText.OperatingSystem(device);

    public static string Model(IntuneDevice? device) => device is null ? "" : DeviceText.Model(device);

    public static string Encrypted(IntuneDevice? device) => device is null ? "" : DeviceText.Encrypted(device);

    public static string LastSignIn(EntraDevice? device) => device is null ? "" : DateText.RelativeAndAbsolute(device.ApproximateLastSignIn);

    // Item templates: the first argument is the item's never-null ID or name.

    public static string RecordChoice(string deviceName, DateTimeOffset? lastSync) =>
        $"{(deviceName.Length == 0 ? "Unnamed device" : deviceName)} ({(lastSync is { } synced ? $"synced {DateText.ShortDate(synced)}" : "never synced")})";

    public static string LastContacted(string id, DateTimeOffset? lastContacted) => DateText.RelativeAndAbsolute(lastContacted);

    public static string SettingDetail(string name, string? currentValue, string? errorDescription) =>
        DeviceText.Join(" · ", currentValue is null ? "" : $"Current value: {currentValue}", errorDescription ?? "");

    public static Visibility SettingDetailVisibility(string name, string? currentValue, string? errorDescription) =>
        VisibleIfText(SettingDetail(name, currentValue, errorDescription));

    public static string StepTitle(int number, string title) => $"{number}. {title}";

    public static string DeviceGlyph(string operatingSystem) =>
        operatingSystem.ToLowerInvariant() switch
        {
            "windows" or "macos" => "",
            "ios" or "ipados" or "android" => "",
            _ => "",
        };

    // Permissions card states.

    public static Visibility PermissionsLoading(PermissionsState state) => Visible(state == PermissionsState.Loading);

    public static Visibility PermissionsLoaded(PermissionsState state) => Visible(state == PermissionsState.Loaded);

    public static Visibility PermissionsUnknown(PermissionsState state) => Visible(state == PermissionsState.Unknown);

    public static Visibility PermissionsFailed(PermissionsState state) => Visible(state == PermissionsState.Failed);

    /// <summary>Anchored on the state, which changes on every refresh, so the time stays current.</summary>
    public static string Refreshed(PermissionsState state, DateTimeOffset? at) => at is { } value ? $"Refreshed {DateText.Relative(value)}." : "";
}
