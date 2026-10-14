using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>beta managed device with <c>hardwareInformation</c>, which v1.0 doesn't expose.</summary>
internal sealed class HardwareDetailsDto
{
    public const string SelectFields = "id,physicalMemoryInBytes,processorArchitecture,hardwareInformation";

    internal sealed class HardwareInformationDto
    {
        public string? OperatingSystemEdition { get; init; }

        public string? OsBuildNumber { get; init; }

        public string? ProductName { get; init; }

        public string? SystemManagementBIOSVersion { get; init; }

        public string? TpmVersion { get; init; }

        public string? TpmSpecificationVersion { get; init; }

        public string? TpmManufacturer { get; init; }

        public double? BatteryHealthPercentage { get; init; }

        public double? BatteryChargeCycles { get; init; }

        public double? BatteryLevelPercentage { get; init; }

        public string? IpAddressV4 { get; init; }

        public string? SubnetAddress { get; init; }

        public IReadOnlyList<string>? WiredIPv4Addresses { get; init; }

        public string? DeviceFullQualifiedDomainName { get; init; }

        public string? SubscriberCarrier { get; init; }

        public string? CellularTechnology { get; init; }

        public string? OperatingSystemLanguage { get; init; }
    }

    public long? PhysicalMemoryInBytes { get; init; }

    public string? ProcessorArchitecture { get; init; }

    public HardwareInformationDto? HardwareInformation { get; init; }

    public HardwareDetails ToDomain()
    {
        var info = HardwareInformation;
        var architecture = ProcessorArchitecture.NonEmpty();
        return new HardwareDetails
        {
            PhysicalMemoryBytes = PhysicalMemoryInBytes is > 0 ? PhysicalMemoryInBytes : null,
            ProcessorArchitecture = string.Equals(architecture, "unknown", StringComparison.OrdinalIgnoreCase) ? null : architecture,
            OperatingSystemEdition = info?.OperatingSystemEdition.NonEmpty(),
            OsBuildNumber = info?.OsBuildNumber.NonEmpty(),
            ProductName = info?.ProductName.NonEmpty(),
            BiosVersion = info?.SystemManagementBIOSVersion.NonEmpty(),
            TpmVersion = info?.TpmSpecificationVersion.NonEmpty() ?? info?.TpmVersion.NonEmpty(),
            TpmManufacturer = info?.TpmManufacturer.NonEmpty(),
            BatteryHealthPercentage = Positive(info?.BatteryHealthPercentage) is { } health
                ? (int)Math.Round(health, MidpointRounding.AwayFromZero)
                : null,
            BatteryChargeCycles = Positive(info?.BatteryChargeCycles) is { } cycles ? (int)cycles : null,
            BatteryLevelPercentage = Positive(info?.BatteryLevelPercentage),
            IpAddress = info?.IpAddressV4.NonEmpty(),
            Subnet = info?.SubnetAddress.NonEmpty(),
            WiredIpAddresses = (info?.WiredIPv4Addresses ?? []).Where(address => address.Length > 0).ToList(),
            FullyQualifiedName = info?.DeviceFullQualifiedDomainName.NonEmpty(),
            Carrier = info?.SubscriberCarrier.NonEmpty(),
            CellularTechnology = info?.CellularTechnology.NonEmpty(),
            Language = info?.OperatingSystemLanguage.NonEmpty(),
        };
    }

    /// <summary>Intune reports "unknown" battery values as 0.</summary>
    private static double? Positive(double? value) => value is > 0 ? value : null;
}
