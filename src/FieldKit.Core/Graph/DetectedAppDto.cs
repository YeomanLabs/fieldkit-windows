using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>beta <c>managedDevices/{id}/detectedApps</c> item.</summary>
internal sealed class DetectedAppDto
{
    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public string? Publisher { get; init; }

    public long? SizeInByte { get; init; }

    public DetectedApp ToDomain() =>
        new(Id, DisplayName ?? "Unnamed app", Version ?? "", Publisher ?? "", SizeInByte is > 0 ? SizeInByte : null);
}
