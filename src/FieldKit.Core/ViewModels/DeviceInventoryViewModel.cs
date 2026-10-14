using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Storage, hardware, battery, network, Defender and installed apps for one device (ports
/// <c>DeviceInventoryModel</c> and the sections of <c>DeviceInventoryView</c>). Each part loads
/// and fails on its own; beta-backed parts (hardware, detected apps) can fail while the rest show.
/// </summary>
public sealed partial class DeviceInventoryViewModel : ObservableObject
{
    private readonly DeviceInventoryLookup _lookup;
    private readonly Action _onUnauthorized;
    private readonly TimeProvider _time;

    public DeviceInventoryViewModel(IntuneDevice device, DeviceInventoryLookup lookup, Action onUnauthorized, TimeProvider? time = null)
    {
        Device = device;
        _lookup = lookup;
        _onUnauthorized = onUnauthorized;
        _time = time ?? TimeProvider.System;
        Inventory = SectionState<DeviceInventory>.Loading;
        Hardware = SectionState<HardwareDetails>.Loading;
        Apps = SectionState<IReadOnlyList<DetectedApp>>.Loading;
        // Defender reports through Intune only for Windows.
        Defender = IsWindows ? SectionState<DefenderStatus>.Loading : null;
    }

    public IntuneDevice Device { get; }

    public bool IsWindows => string.Equals(Device.OperatingSystem, "windows", StringComparison.OrdinalIgnoreCase);

    private bool IsMobile => Device.OperatingSystem.ToLowerInvariant() is "ios" or "ipados" or "android";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Storage), nameof(HasStorage), nameof(NoStorage), nameof(InventorySections))]
    public partial SectionState<DeviceInventory> Inventory { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HardwareRows), nameof(BatteryRows), nameof(HasBatteryRows), nameof(NoBattery), nameof(HardwareLoaded), nameof(ConnectivitySections))]
    public partial SectionState<HardwareDetails> Hardware { get; private set; }

    /// <summary>Null for non-Windows devices, where Defender doesn't report through Intune.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefenderRows), nameof(DefenderState))]
    public partial SectionState<DefenderStatus>? Defender { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppList), nameof(AppsHeader), nameof(FilteredApps))]
    public partial SectionState<IReadOnlyList<DetectedApp>> Apps { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredApps))]
    public partial string AppQuery { get; set; } = "";

    // Storage, network, mobile and management (managedDevice).

    public StorageUsage? Storage => Inventory.Kind == SectionKind.Loaded ? Inventory.Value!.Storage : null;

    public bool HasStorage => Storage is not null;

    /// <summary>Loaded, but Intune didn't report storage.</summary>
    public bool NoStorage => Inventory.Kind == SectionKind.Loaded && Storage is null;

    public IReadOnlyList<InfoSection> InventorySections
    {
        get
        {
            if (Inventory.Kind != SectionKind.Loaded) return [];
            var inventory = Inventory.Value!;
            var sections = new List<InfoSection?>
            {
                InfoSection.From("Network", ("Wi-Fi MAC", inventory.WifiMac), ("Ethernet MAC", inventory.EthernetMac)),
            };
            if (IsMobile)
            {
                sections.Add(InfoSection.From("Mobile",
                    ("IMEI", inventory.Imei),
                    ("Phone number", inventory.PhoneNumber),
                    ("Jailbroken", string.Equals(inventory.JailBroken, "unknown", StringComparison.OrdinalIgnoreCase) ? null : inventory.JailBroken),
                    ("Supervised", inventory.IsSupervised is { } supervised ? (supervised ? "Yes" : "No") : null)));
            }
            sections.Add(InfoSection.From("Management",
                ("Agent", inventory.ManagementAgent is { } agent ? IntuneLabels.ManagementAgent(agent) : null),
                ("Enrollment", inventory.EnrollmentType is { } type ? IntuneLabels.EnrollmentType(type) : null)));
            return sections.OfType<InfoSection>().ToList();
        }
    }

    // Hardware, battery and connectivity (beta hardwareInformation).

    public bool HardwareLoaded => Hardware.Kind == SectionKind.Loaded;

    public IReadOnlyList<InfoRow> HardwareRows
    {
        get
        {
            if (Hardware.Kind != SectionKind.Loaded) return [];
            var hardware = Hardware.Value!;
            return InfoSection.From("Hardware",
                ("Model", DeviceText.Join(" ", Device.Manufacturer, Device.Model)),
                ("Product", hardware.ProductName),
                ("Memory", hardware.PhysicalMemoryBytes is { } bytes ? MemoryText(bytes) : null),
                ("Architecture", hardware.ProcessorArchitecture),
                ("OS", DeviceText.Join(" ", Device.OperatingSystem, hardware.OperatingSystemEdition ?? "")),
                ("OS build", hardware.OsBuildNumber ?? (Device.OsVersion.Length > 0 ? Device.OsVersion : null)),
                ("Language", hardware.Language),
                ("BIOS", hardware.BiosVersion),
                ("TPM", DeviceText.Join(" ", hardware.TpmManufacturer ?? "", hardware.TpmVersion ?? "")),
                ("Encrypted", Device.IsEncrypted is { } encrypted ? (encrypted ? "Yes" : "No") : null))?.Rows ?? [];
        }
    }

    public IReadOnlyList<InfoRow> BatteryRows
    {
        get
        {
            if (Hardware.Kind != SectionKind.Loaded || !Hardware.Value!.HasBattery) return [];
            var hardware = Hardware.Value!;
            var rows = new List<InfoRow>();
            if (hardware.BatteryHealthPercentage is { } health)
            {
                rows.Add(new InfoRow("Health", $"{health}%", health < 60 ? InfoTone.Caution : InfoTone.Normal));
            }
            if (hardware.BatteryChargeCycles is { } cycles) rows.Add(new InfoRow("Charge cycles", cycles.ToString(CultureInfo.CurrentCulture)));
            if (hardware.BatteryLevelPercentage is { } level) rows.Add(new InfoRow("Charge", $"{(int)Math.Round(level)}%"));
            return rows;
        }
    }

    public bool HasBatteryRows => BatteryRows.Count > 0;

    /// <summary>Hardware loaded but there's no battery to report.</summary>
    public bool NoBattery => HardwareLoaded && !Hardware.Value!.HasBattery;

    public IReadOnlyList<InfoSection> ConnectivitySections
    {
        get
        {
            if (Hardware.Kind != SectionKind.Loaded) return [];
            var hardware = Hardware.Value!;
            return InfoSection.From("Connectivity",
                ("IP address", hardware.IpAddress),
                ("Subnet", hardware.Subnet),
                ("Wired IPs", string.Join(", ", hardware.WiredIpAddresses)),
                ("Full name", hardware.FullyQualifiedName),
                ("Carrier", hardware.Carrier),
                ("Cellular", hardware.CellularTechnology)) is { } section ? [section] : [];
        }
    }

    // Defender (Windows only).

    /// <summary>For the card: never null, so bindings update (a non-Windows device hides the card).</summary>
    public ISectionState DefenderState => (ISectionState?)Defender ?? SectionState<DefenderStatus>.Skipped("");

    public IReadOnlyList<InfoRow> DefenderRows
    {
        get
        {
            if (Defender is not { Kind: SectionKind.Loaded } state) return [];
            var defender = state.Value!;
            var rows = new List<InfoRow>();
            void YesNo(string label, bool? value, bool goodWhen)
            {
                if (value is { } flag) rows.Add(new InfoRow(label, flag ? "Yes" : "No", flag == goodWhen ? InfoTone.Good : InfoTone.Caution));
            }
            YesNo("Real-time protection", defender.RealTimeProtectionEnabled, goodWhen: true);
            YesNo("Tamper protection", defender.TamperProtectionEnabled, goodWhen: true);
            YesNo("Malware protection", defender.MalwareProtectionEnabled, goodWhen: true);
            if (defender.SignatureVersion is { } version) rows.Add(new InfoRow("Signatures", version));
            if (defender.SignatureUpdatedAt is { } updated)
            {
                rows.Add(new InfoRow("Last reported", DateText.RelativeAndAbsolute(updated, _time.GetUtcNow()),
                    defender.SignaturesAreStale(_time.GetUtcNow()) ? InfoTone.Caution : InfoTone.Normal));
            }
            rows.Add(new InfoRow("Last quick scan", DateText.RelativeAndAbsolute(defender.LastQuickScanAt, _time.GetUtcNow())));
            rows.Add(new InfoRow("Last full scan", DateText.RelativeAndAbsolute(defender.LastFullScanAt, _time.GetUtcNow())));
            YesNo("Reboot required", defender.RebootRequired, goodWhen: false);
            YesNo("Full scan required", defender.FullScanRequired, goodWhen: false);
            if (defender.DeviceState is { } deviceState) rows.Add(new InfoRow("State", IntuneLabels.Words(deviceState)));
            if (defender.EngineVersion is { } engine) rows.Add(new InfoRow("Engine", engine));
            return rows;
        }
    }

    // Detected apps (beta).

    public IReadOnlyList<DetectedApp> AppList => Apps.Kind == SectionKind.Loaded ? Apps.Value! : [];

    public string AppsHeader => Apps.Kind == SectionKind.Loaded ? $"Apps on device ({AppList.Count})" : "Apps on device";

    /// <summary>Apps whose name or publisher contains the search text (ports <c>DetectedAppsView</c>).</summary>
    public IReadOnlyList<DetectedApp> FilteredApps
    {
        get
        {
            var term = AppQuery.Trim();
            return term.Length == 0
                ? AppList
                : AppList.Where(app => app.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                    || app.Publisher.Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }
    }

    public string AppsFooter => $"Apps found by Intune's inventory on {Device.DeviceName}, however they were installed.";

    [RelayCommand]
    public Task LoadAsync()
    {
        var id = Device.Id;
        return Task.WhenAll(LoadInventoryAsync(id), LoadHardwareAsync(id), LoadDefenderAsync(id), LoadAppsAsync(id));
    }

    /// <summary>"Publisher · 120 MB" under each detected app.</summary>
    public static string AppDetail(DetectedApp app) =>
        DeviceText.Join(" · ", app.Publisher, app.SizeBytes is { } size ? StorageUsage.Format(size) : "");

    /// <summary>Memory in binary units, as iOS's memory byte count style ("16 GB" for 16 GiB).</summary>
    public static string MemoryText(long bytes)
    {
        const double Gib = 1024d * 1024 * 1024;
        const double Mib = 1024d * 1024;
        return bytes >= Gib
            ? string.Create(CultureInfo.CurrentCulture, $"{bytes / Gib:0.#} GB")
            : string.Create(CultureInfo.CurrentCulture, $"{bytes / Mib:0} MB");
    }

    private async Task LoadInventoryAsync(string id)
    {
        Inventory = SectionState<DeviceInventory>.Loading;
        try
        {
            Inventory = SectionState<DeviceInventory>.Loaded(await _lookup.InventoryAsync(id));
        }
        catch (Exception error)
        {
            if (Message(error) is { } message) Inventory = SectionState<DeviceInventory>.Failed(message);
        }
    }

    private async Task LoadHardwareAsync(string id)
    {
        Hardware = SectionState<HardwareDetails>.Loading;
        try
        {
            Hardware = SectionState<HardwareDetails>.Loaded(await _lookup.HardwareAsync(id));
        }
        catch (Exception error)
        {
            if (Message(error) is { } message) Hardware = SectionState<HardwareDetails>.Failed(message);
        }
    }

    private async Task LoadDefenderAsync(string id)
    {
        if (Defender is null) return;
        Defender = SectionState<DefenderStatus>.Loading;
        try
        {
            Defender = await _lookup.DefenderAsync(id) is { } status
                ? SectionState<DefenderStatus>.Loaded(status)
                : SectionState<DefenderStatus>.NotFound;
        }
        catch (Exception error)
        {
            if (Message(error) is { } message) Defender = SectionState<DefenderStatus>.Failed(message);
        }
    }

    private async Task LoadAppsAsync(string id)
    {
        Apps = SectionState<IReadOnlyList<DetectedApp>>.Loading;
        try
        {
            var found = await _lookup.DetectedAppsAsync(id);
            Apps = found.Count == 0
                ? SectionState<IReadOnlyList<DetectedApp>>.NotFound
                : SectionState<IReadOnlyList<DetectedApp>>.Loaded(found);
        }
        catch (Exception error)
        {
            if (Message(error) is { } message) Apps = SectionState<IReadOnlyList<DetectedApp>>.Failed(message);
        }
    }

    private string? Message(Exception error) => FeatureErrors.Message(error, _onUnauthorized);
}
