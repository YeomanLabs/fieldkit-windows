namespace FieldKit.Core.Domain;

public sealed record EntraDevice
{
    public required string Id { get; init; }

    public string DisplayName { get; init; } = "";

    public bool AccountEnabled { get; init; } = true;

    public DateTimeOffset? ApproximateLastSignIn { get; init; }

    public EntraTrustType TrustType { get; init; } = EntraTrustType.None;

    public string OperatingSystem { get; init; } = "";

    /// <summary>extensionAttribute1–15 (Windows only).</summary>
    public DeviceExtensionAttributes ExtensionAttributes { get; init; } = DeviceExtensionAttributes.None;
}
