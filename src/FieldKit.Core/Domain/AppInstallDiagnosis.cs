namespace FieldKit.Core.Domain;

/// <summary>Why an app is or isn't installed on a device: the verdict, the likely causes in order, and what to check.</summary>
public sealed record AppInstallDiagnosis(
    AppInstallVerdict Verdict,
    string Title,
    string Explanation,
    IReadOnlyList<AppInstallCause> Causes,
    IReadOnlyList<string> WhatToCheck)
{
    public string Label => Verdict switch
    {
        AppInstallVerdict.Installed => "Installed",
        AppInstallVerdict.Uninstalled => "Uninstalled",
        AppInstallVerdict.Available => "Available",
        AppInstallVerdict.Waiting => "Waiting",
        AppInstallVerdict.NotInstalled => "Not installed",
        AppInstallVerdict.NotApplicable => "Not applicable",
        _ => "Failed",
    };

    public bool IsProblem => Verdict is AppInstallVerdict.Failed or AppInstallVerdict.NotApplicable or AppInstallVerdict.NotInstalled or AppInstallVerdict.Waiting;
}
