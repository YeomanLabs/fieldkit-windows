using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>What the install status tab hands "Why didn't it install?": one app on one device.</summary>
/// <param name="EntraObjectId">The device's Entra object, for its groups; null when it has none.</param>
/// <param name="DeviceApps">Every app on the tab, to see how this one's dependencies are doing.</param>
public sealed record AppInstallSubject(IntuneDevice Device, string? EntraObjectId, AppInstallStatus App, IReadOnlyList<AppInstallStatus> DeviceApps);
