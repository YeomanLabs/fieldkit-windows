using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>One app in <c>mobileAppIntentAndState.mobileAppList</c>.</summary>
internal sealed class MobileAppIntentItemDto
{
    public string? ApplicationId { get; init; }

    public string? DisplayName { get; init; }

    public string? MobileAppIntent { get; init; }

    public string? DisplayVersion { get; init; }

    public string? InstallState { get; init; }

    public AppInstallStatus ToDomain() =>
        new()
        {
            AppId = ApplicationId ?? "",
            Name = DisplayName ?? "Unnamed app",
            Version = DisplayVersion ?? "",
            Intent = AppIntents.FromRaw(MobileAppIntent),
            State = AppInstallStates.FromRaw(InstallState),
        };
}
