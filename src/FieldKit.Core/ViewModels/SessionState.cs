namespace FieldKit.Core.ViewModels;

/// <summary>Where the app is in setup and sign-in (the cases of iOS <c>AppSession.State</c>).</summary>
public enum SessionState
{
    Restoring,
    /// <summary>No tenant configured yet, or the user is changing it.</summary>
    NeedsSetup,
    SignedOut,
    SignedIn,
}
