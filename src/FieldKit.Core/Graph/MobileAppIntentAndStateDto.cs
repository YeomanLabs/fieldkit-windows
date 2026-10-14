namespace FieldKit.Core.Graph;

/// <summary><c>GET /beta/users('{userId}')/mobileAppIntentAndStates('{managedDeviceId}')</c>.</summary>
internal sealed class MobileAppIntentAndStateDto
{
    public string? Id { get; init; }

    public string? ManagedDeviceIdentifier { get; init; }

    public List<MobileAppIntentItemDto>? MobileAppList { get; init; }
}
