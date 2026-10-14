using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>One sign-in in a person's list: the app, when, from where, and the verdict.</summary>
public sealed record SignInItem(SignIn SignIn, SignInDiagnosis Diagnosis, string When)
{
    public static SignInItem From(SignIn signIn, DateTimeOffset now) =>
        new(signIn, SignInDecoder.Diagnose(signIn), DateText.Relative(signIn.CreatedAt, now));

    public string App => SignIn.AppName.Length > 0 ? SignIn.AppName : SignIn.ResourceName.Length > 0 ? SignIn.ResourceName : "Unknown app";

    /// <summary>"2 hours ago · CTS-LT-01188 · Redmond, Washington, US".</summary>
    public string Subtitle =>
        DeviceText.Join(" · ", When, SignIn.Device.DisplayName.Length > 0 ? SignIn.Device.DisplayName : SignIn.Device.OperatingSystem, SignIn.Location);

    /// <summary>The reason in a few words, for anything but a success.</summary>
    public string Reason => Diagnosis.IsProblem ? Diagnosis.Title : "";

    public SignInVerdict Verdict => Diagnosis.Verdict;

    public string Label => Diagnosis.Label;

    public string AutomationName => Diagnosis.IsProblem ? $"{Label}: {App}, {Subtitle}. {Reason}." : $"{Label}: {App}, {Subtitle}.";
}
