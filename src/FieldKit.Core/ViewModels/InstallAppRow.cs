using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>An app in the install list, with whether it's already required here (ports <c>InstallAppRow</c>).</summary>
public sealed record InstallAppRow(AssignedItem App, AppInstallPlan.Status Status)
{
    public string Name => App.Name;

    public string Detail => App.Detail;

    public bool HasDetail => App.Detail.Length > 0;

    public bool IsRequired => Status.Kind == AppInstallPlan.StatusKind.Required;

    public bool IsExcluded => Status.Kind == AppInstallPlan.StatusKind.Excluded;

    public string Badge => IsRequired ? "Required" : IsExcluded ? "Excluded" : "";

    public bool HasBadge => Badge.Length > 0;

    public string StatusText => Status.Kind switch
    {
        AppInstallPlan.StatusKind.Required => $"Already required via {string.Join(", ", Status.Via)}",
        AppInstallPlan.StatusKind.Excluded => $"Excluded via {string.Join(", ", Status.Via)}",
        _ => "",
    };

    public bool HasStatusText => StatusText.Length > 0;

    public string AutomationName => DeviceText.Join(", ", Name, Detail, StatusText);
}
