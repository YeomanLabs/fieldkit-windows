using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>A sign-in's <c>deviceDetail</c>.</summary>
internal sealed class SignInDeviceDetailDto
{
    public string? DeviceId { get; init; }

    public string? DisplayName { get; init; }

    public string? OperatingSystem { get; init; }

    public string? Browser { get; init; }

    public bool? IsCompliant { get; init; }

    public bool? IsManaged { get; init; }

    public string? TrustType { get; init; }

    public SignInDevice ToDomain() =>
        new()
        {
            DeviceId = DeviceId ?? "",
            DisplayName = DisplayName ?? "",
            OperatingSystem = OperatingSystem ?? "",
            Browser = Browser ?? "",
            IsCompliant = IsCompliant,
            IsManaged = IsManaged,
            TrustType = TrustType ?? "",
        };
}
