using System.Text.Json.Serialization;

namespace FieldKit.Core.Graph;

/// <summary>
/// <c>GET /informationProtection/bitlocker/recoveryKeys</c> item. <c>key</c> is only present when
/// requested with <c>$select=key</c>.
/// </summary>
internal sealed class BitLockerRecoveryKeyDto
{
    public string? Id { get; init; }

    public DateTimeOffset? CreatedDateTime { get; init; }

    /// <summary>Documented as an enum but returned as "1" in examples; a string or a number is accepted.</summary>
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? VolumeType { get; init; }

    public string? DeviceId { get; init; }

    public string? Key { get; init; }
}
