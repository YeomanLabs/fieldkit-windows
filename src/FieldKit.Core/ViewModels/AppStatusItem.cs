using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>One app on the install status tab: name, how it's assigned, whether it installed, and why not.</summary>
public sealed record AppStatusItem(AppInstallStatus App, string When)
{
    public static AppStatusItem From(AppInstallStatus app, DateTimeOffset now) =>
        new(app, app.Failure?.At is { } at ? DateText.Relative(at, now) : "");

    public string Name => App.Name;

    /// <summary>"Required · 6.8.28".</summary>
    public string Subtitle => DeviceText.Join(" · ", App.Intent.DisplayName(), App.Version.Length > 0 && !App.Name.Contains(App.Version, StringComparison.Ordinal) ? App.Version : "");

    public string StateText => App.Outcome.DisplayName(App.Intent);

    public InfoTone Tone => App.Outcome switch
    {
        AppInstallOutcome.Failed => InfoTone.Critical,
        AppInstallOutcome.Pending or AppInstallOutcome.Missing or AppInstallOutcome.NoStatus => InfoTone.Caution,
        AppInstallOutcome.Installed or AppInstallOutcome.Removed => InfoTone.Good,
        _ => InfoTone.Normal,
    };

    /// <summary>"0x87D1041C · The app wasn't detected ... · 3 hours ago".</summary>
    public string ErrorText => App.Failure is { } failure ? DeviceText.Join(" · ", failure.Summary, When) : "";

    public bool HasError => ErrorText.Length > 0;

    /// <summary>Intune's own words for the failure, when it gave any.</summary>
    public string Detail => App.Failure?.Detail ?? "";

    public bool HasDetail => Detail.Length > 0;

    public string AutomationName => DeviceText.Join(". ", $"{Name}: {StateText}", Subtitle, ErrorText, Detail);
}
