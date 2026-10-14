namespace FieldKit.Core.Domain;

/// <summary>The device a sign-in came from, as Entra saw it. Every part can be missing.</summary>
public sealed record SignInDevice
{
    /// <summary>The Entra device ID (Intune's <c>azureADDeviceId</c>); empty when the sign-in had no device identity.</summary>
    public string DeviceId { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string OperatingSystem { get; init; } = "";

    public string Browser { get; init; } = "";

    public bool? IsCompliant { get; init; }

    public bool? IsManaged { get; init; }

    /// <summary>"Azure AD joined", "Hybrid Azure AD joined", "Azure AD registered", or empty.</summary>
    public string TrustType { get; init; } = "";

    public bool HasIdentity => DeviceId.Length > 0;
}
