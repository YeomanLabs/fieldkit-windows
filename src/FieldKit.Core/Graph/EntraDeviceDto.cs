using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary><c>GET /devices</c> item.</summary>
internal sealed class EntraDeviceDto
{
    public static readonly string SelectFields = string.Join(',',
        "id", "displayName", "accountEnabled", "approximateLastSignInDateTime", "trustType", "operatingSystem", "extensionAttributes");

    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public bool? AccountEnabled { get; init; }

    public DateTimeOffset? ApproximateLastSignInDateTime { get; init; }

    public string? TrustType { get; init; }

    public string? OperatingSystem { get; init; }

    public ExtensionAttributesDto? ExtensionAttributes { get; init; }

    public EntraDevice ToDomain() =>
        new()
        {
            Id = Id,
            DisplayName = DisplayName ?? "",
            // Graph omits accountEnabled only in unusual cases; don't invent a disabled flag.
            AccountEnabled = AccountEnabled ?? true,
            ApproximateLastSignIn = GraphJson.Meaningful(ApproximateLastSignInDateTime),
            TrustType = EntraTrustType.FromRaw(TrustType),
            OperatingSystem = OperatingSystem ?? "",
            ExtensionAttributes = ExtensionAttributes?.ToDomain() ?? DeviceExtensionAttributes.None,
        };
}
