namespace FieldKit.Core.Domain;

public static class DeviceActionExtensions
{
    public static string Title(this DeviceAction action) =>
        action switch
        {
            DeviceAction.Sync => "Sync",
            DeviceAction.Restart => "Restart",
            DeviceAction.Retire => "Retire",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    /// <summary>Sync is harmless; everything else needs Windows Hello.</summary>
    public static bool RequiresOwnerVerification(this DeviceAction action) => action != DeviceAction.Sync;

    /// <summary>Retire also needs the device name typed back.</summary>
    public static bool RequiresTypedConfirmation(this DeviceAction action) => action == DeviceAction.Retire;

    public static string ConfirmationMessage(this DeviceAction action) =>
        action switch
        {
            DeviceAction.Sync => "Ask the device to check in with Intune now.",
            DeviceAction.Restart => "The device restarts as soon as it receives the command. Unsaved work on it is lost.",
            DeviceAction.Retire => "Removes company data, apps and settings and unenrolls the device from Intune. Personal data is kept. This can't be undone.",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    public static string SuccessMessage(this DeviceAction action) =>
        action switch
        {
            DeviceAction.Sync => "Sync sent. The device checks in when it's online.",
            DeviceAction.Restart => "Restart sent. The device restarts when it receives it.",
            DeviceAction.Retire => "Retire sent. Intune removes company data when the device checks in.",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
