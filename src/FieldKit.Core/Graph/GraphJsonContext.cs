using System.Text.Json.Serialization;

namespace FieldKit.Core.Graph;

/// <summary>
/// Source-generated JSON metadata for every Graph response shape FieldKit reads. Add a
/// response type here (and a fixture under FieldkitTests/Fixtures) when adding a call.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    Converters = [typeof(GraphDateConverter)])]
[JsonSerializable(typeof(GraphErrorBody))]
[JsonSerializable(typeof(GraphCollection<OrganizationDto>))]
[JsonSerializable(typeof(GraphCollection<ManagedDeviceDto>))]
[JsonSerializable(typeof(ManagedDeviceDto))]
[JsonSerializable(typeof(GraphCollection<EntraDeviceDto>))]
[JsonSerializable(typeof(GraphCollection<AutopilotDeviceIdentityDto>))]
[JsonSerializable(typeof(GraphCollection<CompliancePolicyStateDto>))]
[JsonSerializable(typeof(GraphCollection<UserDto>))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(GraphCollection<DirectoryObjectDto>))]
[JsonSerializable(typeof(DirectoryObjectDto))]
[JsonSerializable(typeof(GraphCollection<AssignableObjectDto>))]
[JsonSerializable(typeof(GraphCollection<AssignmentDto>))]
[JsonSerializable(typeof(DeviceLocalCredentialInfoDto))]
[JsonSerializable(typeof(GraphCollection<BitLockerRecoveryKeyDto>))]
[JsonSerializable(typeof(BitLockerRecoveryKeyDto))]
[JsonSerializable(typeof(DeviceInventoryDto))]
[JsonSerializable(typeof(HardwareDetailsDto))]
[JsonSerializable(typeof(DefenderStatusDto))]
[JsonSerializable(typeof(GraphCollection<DetectedAppDto>))]
[JsonSerializable(typeof(GraphCollection<SignInDto>))]
[JsonSerializable(typeof(MobileAppIntentAndStateDto))]
[JsonSerializable(typeof(MobileAppDto))]
[JsonSerializable(typeof(AssignmentFilterDto))]
internal sealed partial class GraphJsonContext : JsonSerializerContext
{
}
