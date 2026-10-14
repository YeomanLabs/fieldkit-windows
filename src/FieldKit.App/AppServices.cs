using FieldKit.Core.Persistence;
using FieldKit.Core.Services;
using FieldKit.Core.ViewModels;

namespace FieldKit.App;

/// <summary>The app-wide services pages are built from, created once at launch.</summary>
internal sealed class AppServices
{
    public static AppServices Current { get; set; } = null!;

    public required ILocalStore Store { get; init; }

    public required SettingsStore Settings { get; init; }

    public required AppSession Session { get; init; }

    public required ILocalDeviceInfo DeviceInfo { get; init; }

    /// <summary>Where <c>fieldkit.db</c> lives, shown in Settings.</summary>
    public required string DataFolder { get; init; }

    /// <summary>"1.0.0", from the assembly.</summary>
    public required string Version { get; init; }

    /// <summary>Windows Hello before restart, retire, remediations and secrets.</summary>
    public required IOwnerVerifier OwnerVerifier { get; init; }

    /// <summary>Copies secrets out of clipboard history and clears them after 60 seconds.</summary>
    public required Platform.SecureClipboard Clipboard { get; init; }
}
