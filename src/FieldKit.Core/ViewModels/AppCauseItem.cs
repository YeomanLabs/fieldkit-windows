using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>One numbered cause on "Why didn't it install?", tagged Likely or Possible.</summary>
public sealed record AppCauseItem(int Number, AppInstallCause Cause)
{
    public string NumberText => $"{Number}.";

    public string Title => Cause.Title;

    public string Detail => Cause.Detail;

    public string Badge => Cause.Likely ? "Likely" : "Possible";

    public InfoTone Tone => Cause.Likely ? InfoTone.Caution : InfoTone.Normal;

    public string AutomationName => $"{Number}. {Badge}: {Title}. {Detail}";
}
