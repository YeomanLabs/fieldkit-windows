using System.Diagnostics;
using FieldKit.Core.Services;
using Windows.Security.Credentials.UI;

namespace FieldKit.App.Platform;

/// <summary>
/// Windows Hello (face, fingerprint or PIN) before restart, retire, remediations and recovery
/// secrets (SPEC-windows §8). WinUI 3 windows have no CoreWindow, so the prompt is parented to
/// the window handle through <c>UserConsentVerifierInterop</c>. Never skipped: without Hello,
/// it explains how to set up a PIN.
/// </summary>
internal sealed class WindowsHelloVerifier(Func<IntPtr> window) : IOwnerVerifier
{
    public const string SetUpMessage =
        "Set up Windows Hello to use this: Settings → Accounts → Sign-in options → PIN (Windows Hello). A PIN is enough.";

    public async Task<string?> VerifyAsync(string reason)
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            if (availability != UserConsentVerifierAvailability.Available) return Message(availability);
            var result = await UserConsentVerifierInterop.RequestVerificationForWindowAsync(window(), reason);
            return Message(result);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Debug.WriteLine($"Windows Hello failed: {error.GetType().Name}");
            return "Couldn't confirm it's you with Windows Hello. Try again.";
        }
    }

    private static string? Message(UserConsentVerificationResult result) =>
        result switch
        {
            UserConsentVerificationResult.Verified => null,
            UserConsentVerificationResult.Canceled => "Cancelled.",
            UserConsentVerificationResult.DeviceBusy => "Windows Hello is busy. Try again in a moment.",
            UserConsentVerificationResult.RetriesExhausted => "Too many attempts. Unlock Windows Hello, then try again.",
            UserConsentVerificationResult.DisabledByPolicy => "Windows Hello is turned off by your organisation's policy, so FieldKit can't confirm it's you.",
            UserConsentVerificationResult.DeviceNotPresent or UserConsentVerificationResult.NotConfiguredForUser => SetUpMessage,
            _ => "Couldn't confirm it's you with Windows Hello.",
        };

    private static string Message(UserConsentVerifierAvailability availability) =>
        availability switch
        {
            UserConsentVerifierAvailability.DisabledByPolicy => "Windows Hello is turned off by your organisation's policy, so FieldKit can't confirm it's you.",
            UserConsentVerifierAvailability.DeviceBusy => "Windows Hello is busy. Try again in a moment.",
            _ => SetUpMessage,
        };
}
