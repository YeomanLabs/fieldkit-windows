namespace FieldKit.Core.Services;

/// <summary>
/// Confirms the person at the PC is its owner before a sensitive action (ports
/// <c>DeviceOwnerAuthenticating</c>; Windows Hello in the app).
/// </summary>
public interface IOwnerVerifier
{
    /// <summary>Asks for Windows Hello. Returns why it failed, or null when verified.</summary>
    /// <param name="reason">What's being unlocked, e.g. "Show the LAPS password for DESKTOP-1".</param>
    Task<string?> VerifyAsync(string reason);
}
