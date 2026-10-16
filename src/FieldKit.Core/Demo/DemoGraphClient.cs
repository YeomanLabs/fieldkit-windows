using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FieldKit.Core.Graph;

namespace FieldKit.Core.Demo;

/// <summary>
/// Demo mode's canned Graph responses. Nothing leaves the PC. Serials:
/// <list type="bullet">
/// <item><c>5CG1234XYZ</c>: healthy, hybrid joined, LAPS and BitLocker available</item>
/// <item><c>PF2ABCDE</c>: two Intune records, stale, no user, disabled Entra object, failing compliance</item>
/// <item><c>PF9NEW01</c>: Autopilot only</item>
/// <item><c>DENIED01</c>: Intune returns 403</item>
/// <item><c>BOX…</c>: a new, untagged Autopilot device, for pre-staging</item>
/// <item>anything else: unknown</item>
/// </list>
/// People and groups match on the start of a word ("pri", "kio"). Device names: "CTS-…" and "DESKTOP-…". Writes return 204.
/// </summary>
public sealed partial class DemoGraphClient : IGraphClient
{
    private readonly TimeSpan _delay;

    /// <param name="delay">How long each call pretends to take; tests pass zero.</param>
    public DemoGraphClient(TimeSpan? delay = null)
    {
        _delay = delay ?? TimeSpan.FromMilliseconds(400);
    }

    public async Task<byte[]> SendAsync(GraphRequest request, CancellationToken cancellationToken = default)
    {
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
        }
        return Encoding.UTF8.GetBytes(Respond(request));
    }

    private static string Respond(GraphRequest request)
    {
        var path = request.Url.AbsolutePath
            .Replace("/v1.0/", "", StringComparison.Ordinal)
            .Replace("/beta/", "", StringComparison.Ordinal);
        var query = Uri.UnescapeDataString(request.Url.Query.TrimStart('?'));
        if (request.Method == GraphMethod.Get && FleetResponse(path, query) is { } fleet) return fleet;
        // Install status (beta): app intents per device, and Intune's report actions (reads sent as POST).
        if (request.Method == GraphMethod.Get && IntentsRoute().Match(Uri.UnescapeDataString(path)) is { Success: true } intents) return DemoAppIntents(intents.Groups[1].Value);
        // "Why didn't it install?" (beta): one app with its assignments and relationships, or its Win32 settings.
        if (request.Method == GraphMethod.Get && path.StartsWith("deviceAppManagement/mobileApps/", StringComparison.Ordinal)) return DemoApp(path, query);
        if (request.Method == GraphMethod.Get && path == "deviceManagement/assignmentFilters/flt-desktops") return DesktopsFilter;
        if (request.Method == GraphMethod.Post && path.StartsWith("deviceManagement/reports/", StringComparison.Ordinal))
        {
            // The filter as Graph reads it: JSON writers escape the quotes (') in the raw body.
            using var report = System.Text.Json.JsonDocument.Parse(request.Body ?? "{}"u8.ToArray());
            return DemoReport(path, report.RootElement.TryGetProperty("filter", out var filter) ? filter.GetString() ?? "" : "");
        }

        switch (request.Method)
        {
            case GraphMethod.Delete:
                return path.StartsWith("groups/", StringComparison.Ordinal) ? "" : throw GraphException.NotFound();
            case GraphMethod.Post:
                return path.StartsWith("deviceManagement/", StringComparison.Ordinal) || path.StartsWith("groups/", StringComparison.Ordinal)
                    ? ""
                    : throw GraphException.NotFound();
            case GraphMethod.Patch:
                // Extension attributes; the device page shows what was saved without re-reading.
                return path.StartsWith("devices/", StringComparison.Ordinal) ? "" : throw GraphException.NotFound();
        }

        switch (path)
        {
            case "organization":
                return """{"value":[{"id":"demo","displayName":"Contoso (demo)"}]}""";

            case "deviceManagement/managedDevices":
                if (query.Contains("DENIED01", StringComparison.Ordinal)) throw GraphException.Forbidden();
                // Opening a device from a group's members filters by Entra device ID. Match the
                // filter, not the field name: every lookup's $select includes azureADDeviceId.
                if (query.Contains("azureADDeviceId eq", StringComparison.Ordinal))
                {
                    return Collection(query.Contains("c0a8e2f4", StringComparison.Ordinal) ? [Healthy] : []);
                }
                if (NamePrefix().Match(query) is { Success: true } prefix)
                {
                    var start = prefix.Groups[1].Value.ToUpperInvariant();
                    return Collection(AllDevices.Where(record =>
                        DeviceName().Match(record) is { Success: true } name
                        && name.Groups[1].Value.ToUpperInvariant().StartsWith(start, StringComparison.Ordinal)));
                }
                if (query.Contains("'5CG1234XYZ'", StringComparison.Ordinal)) return Collection([Healthy]);
                if (query.Contains("'PF2ABCDE'", StringComparison.Ordinal)) return Collection(Duplicates);
                // The Devices tab: everything, A–Z.
                if (!query.Contains("$filter=", StringComparison.Ordinal))
                {
                    return Collection(AllDevices.OrderBy(record => DeviceName().Match(record).Groups[1].Value, StringComparer.OrdinalIgnoreCase));
                }
                return Collection([]);

            case "devices":
                if (query.Contains("c0a8e2f4", StringComparison.Ordinal)) return Collection([EntraHealthy]);
                if (query.Contains("d1b2c3d4", StringComparison.Ordinal)) return Collection([EntraDisabled]);
                return Collection([]);

            case "deviceManagement/windowsAutopilotDeviceIdentities":
                if (query.Contains("'5CG1234XYZ'", StringComparison.Ordinal)) return Collection([Autopilot("5CG1234XYZ", "enrolled")]);
                if (query.Contains("'PF9NEW01'", StringComparison.Ordinal)) return Collection([Autopilot("PF9NEW01", "notContacted")]);
                if (BoxSerial().Match(query) is { Success: true } box) return Collection([Autopilot(box.Groups[1].Value, "notContacted")]);
                return Collection([]);

            case "users":
                // The Users tab lists everyone; a search matches the start of a word, as Graph's $search does.
                if (SearchedTerm().Match(query) is not { Success: true } userTerm) return Collection(People.Select(person => person.Json));
                return Collection(People.Where(person => StartsAWord(person.Json, userTerm.Groups[1].Value)).Select(person => person.Json));

            case "auditLogs/signIns":
                return Collection(DemoSignIns(SignInUser().Match(query) is { Success: true } who ? who.Groups[1].Value : ""));

            case "deviceManagement/managedDevices/i-1/deviceCompliancePolicyStates":
                return Collection([Policy("Windows baseline", "compliant", [])]);
            case "deviceManagement/managedDevices/i-2/deviceCompliancePolicyStates":
                return Collection(
                [
                    Policy("Windows baseline", "nonCompliant",
                    [
                        ("Windows10CompliancePolicy.bitLockerEnabled", "", "False"),
                        ("Windows10CompliancePolicy.osMinimumVersion", "Minimum OS version", "10.0.22631.4037"),
                    ]),
                    Policy("Defender for Endpoint risk", "compliant", []),
                    Policy("macOS baseline", "notApplicable", []),
                ]);

            // iOS answers 404 here, which shows as a failure; the record is in its grace period.
            case "deviceManagement/managedDevices/i-3/deviceCompliancePolicyStates":
                return Collection([Policy("Windows baseline", "inGracePeriod", [])]);

            case "directory/deviceLocalCredentials/c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b":
                var password = Convert.ToBase64String(Encoding.UTF8.GetBytes("Kp7#vQ2m-Lx9!dRw"));
                return $$"""
                    {"id":"c0a8e2f4","deviceName":"CTS-LT-01234","lastBackupDateTime":"{{Recent}}","refreshDateTime":"{{Soon}}",
                     "credentials":[{"accountName":"LocalAdmin","backupDateTime":"{{Recent}}","passwordBase64":"{{password}}"}]}
                    """;

            case "informationProtection/bitlocker/recoveryKeys":
                return query.Contains("c0a8e2f4", StringComparison.Ordinal)
                    ? Collection(["""{"id":"b7f3a1c2-0000-4000-8000-000000000001","createdDateTime":"2025-03-04T09:20:00Z","volumeType":"1","deviceId":"c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b"}"""])
                    : Collection([]);
            case "informationProtection/bitlocker/recoveryKeys/b7f3a1c2-0000-4000-8000-000000000001":
                return """{"id":"b7f3a1c2-0000-4000-8000-000000000001","createdDateTime":"2025-03-04T09:20:00Z","volumeType":"1","deviceId":"c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b","key":"118657-202301-591943-336754-218812-490765-611820-264407"}""";

            case "groups":
                if (SearchedTerm().Match(query) is not { Success: true } groupTerm)
                {
                    return Collection(Groups.Select(group => group.Json).OrderBy(json => GroupName().Match(json).Groups[1].Value, StringComparer.OrdinalIgnoreCase));
                }
                return Collection(Groups.Where(group => StartsAWord(group.Json, groupTerm.Groups[1].Value)).Select(group => group.Json));
            case "groups/g-laptops":
                return Group("cts");
            case "groups/g-finance":
                return Group("finance");
            case "groups/g-kiosk":
                return Group("kiosk");
            case "groups/g-7zip":
                return Group("7zip");
            case "users/alex.doe@contoso.com":
                return Person("alex");

            case "deviceManagement/managedDevices/i-1":
                if (query.Contains("hardwareInformation", StringComparison.Ordinal))
                {
                    return """
                        {"id":"i-1","physicalMemoryInBytes":17179869184,"processorArchitecture":"x64","hardwareInformation":{
                         "operatingSystemEdition":"Enterprise","osBuildNumber":"26100.4652","productName":"HP EliteBook 840 G10 Notebook PC",
                         "systemManagementBIOSVersion":"V70 Ver. 01.07.10","tpmSpecificationVersion":"2.0","tpmManufacturer":"IFX",
                         "batteryHealthPercentage":87,"batteryChargeCycles":214,"batteryLevelPercentage":0,
                         "ipAddressV4":"10.20.30.41","subnetAddress":"10.20.30.0","wiredIPv4Addresses":[],
                         "deviceFullQualifiedDomainName":"CTS-LT-01234.contoso.local","operatingSystemLanguage":"en-GB"}}
                        """;
                }
                return """
                    {"id":"i-1","totalStorageSpaceInBytes":255013683200,"freeStorageSpaceInBytes":120259084288,
                     "wiFiMacAddress":"a1b2c3d4e5f6","ethernetMacAddress":"","imei":"","phoneNumber":"",
                     "isSupervised":false,"jailBroken":"Unknown","managementAgent":"mdm","deviceEnrollmentType":"windowsAzureADJoin"}
                    """;
            case "deviceManagement/managedDevices/i-1/windowsProtectionState":
                return $$"""
                    {"realTimeProtectionEnabled":true,"malwareProtectionEnabled":true,"tamperProtectionEnabled":true,
                     "networkInspectionSystemEnabled":true,"signatureVersion":"1.421.1234.0","lastReportedDateTime":"{{Recent}}",
                     "lastQuickScanDateTime":"{{Recent}}","lastFullScanDateTime":null,"rebootRequired":false,"fullScanRequired":false,
                     "deviceState":"clean","engineVersion":"1.1.25090.3001","antiMalwareVersion":"4.18.25090.3009"}
                    """;
            case "deviceManagement/managedDevices/i-1/detectedApps":
                return Collection(
                [
                    """{"id":"d-1","displayName":"Google Chrome","version":"141.0.7390.55","publisher":"Google LLC","sizeInByte":0}""",
                    """{"id":"d-2","displayName":"Microsoft 365 Apps for enterprise","version":"16.0.19127.20146","publisher":"Microsoft Corporation","sizeInByte":0}""",
                    """{"id":"d-3","displayName":"7-Zip","version":"25.01","publisher":"Igor Pavlov","sizeInByte":5242880}""",
                    """{"id":"d-4","displayName":"Company Portal","version":"11.2.1495.0","publisher":"Microsoft Corporation","sizeInByte":0}""",
                    """{"id":"d-5","displayName":"Adobe Acrobat Reader","version":"25.001.20756","publisher":"Adobe","sizeInByte":0}""",
                    """{"id":"d-6","displayName":"Zoom Workplace","version":"6.6.2","publisher":"Zoom Video Communications","sizeInByte":0}""",
                ]);
            case "devices/e-1/memberOf":
                return Collection([Group("kiosk")]);
            case "users/alex.doe@contoso.com/memberOf":
                return Collection([Group("finance")]);
            case "devices/e-1/transitiveMemberOf":
                return Collection([Group("cts"), Group("kiosk")]);
            case "users/alex.doe@contoso.com/transitiveMemberOf" or "users/u-alex/transitiveMemberOf":
                return Collection([Group("finance"), """{"@odata.type":"#microsoft.graph.directoryRole","id":"r-1","displayName":"Helpdesk Administrator"}"""]);
            case "groups/g-laptops/members":
                return Collection(
                [
                    """{"@odata.type":"#microsoft.graph.device","id":"e-1","displayName":"CTS-LT-01234","deviceId":"c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b","operatingSystem":"Windows"}""",
                    """{"@odata.type":"#microsoft.graph.user","id":"u-alex","displayName":"Alex Doe","userPrincipalName":"alex.doe@contoso.com"}""",
                    """{"@odata.type":"#microsoft.graph.group","id":"g-kiosk","displayName":"Kiosk Devices"}""",
                ]);

            case "deviceAppManagement/mobileApps":
                return Collection(
                [
                    Assignable("a-1", "Company Portal", type: "winGetApp", targets: [("allDevices", null, "required")]),
                    Assignable("a-2", "Google Chrome", type: "win32LobApp", targets: [("group", "g-laptops", "required"), ("exclusion", "g-kiosk", null)]),
                    Assignable("a-3", "Adobe Acrobat Reader", type: "win32LobApp", targets: [("group", "g-finance", "available")]),
                    Assignable("a-4", "Old VPN client", type: "win32LobApp", targets: [("group", "g-finance", "uninstall")]),
                    Assignable("a-5", "Unassigned tool", type: "win32LobApp", targets: []),
                    Assignable("a-6", "7-Zip", type: "winGetApp", targets: [("group", "g-7zip", "required"), ("group", "g-laptops", "available")]),
                    Assignable("a-7", "Outlook", type: "iosVppApp", targets: [("allUsers", null, "required")]),
                ]);
            case "deviceManagement/deviceConfigurations":
                return Collection(
                [
                    Assignable("c-1", "Corporate Wi-Fi", targets: [("group", "g-laptops", null)]),
                    Assignable("c-2", "Always On VPN", targets: [("allUsers", null, null)]),
                ]);
            case "deviceManagement/deviceCompliancePolicies":
                return Collection(
                [
                    Assignable("p-1", "Windows baseline", targets: [("allDevices", null, null)]),
                    Assignable("p-2", "Developer tools policy", targets: [("group", "g-7zip", null)]),
                ]);
            case "deviceManagement/configurationPolicies":
                return Collection(["""{"id":"s-1","name":"Edge security baseline","assignments":[{"target":{"@odata.type":"#microsoft.graph.groupAssignmentTarget","groupId":"g-laptops"}}]}"""]);
            case "deviceManagement/deviceManagementScripts":
                return Collection([Assignable("sc-1", "Map finance drives", targets: [("group", "g-finance", null)])]);
            case "deviceManagement/deviceHealthScripts":
                return Collection(
                [
                    Assignable("r-1", "Restart print spooler", publisher: "IT", targets: [("group", "g-laptops", null)]),
                    Assignable("r-2", "Clear Teams cache", publisher: "IT", targets: []),
                ]);

            // Anyone else in the demo directory: in no groups (rather than a 404 the real
            // directory wouldn't give for an object that exists).
            case var other when other.EndsWith("/memberOf", StringComparison.Ordinal) || other.EndsWith("/transitiveMemberOf", StringComparison.Ordinal):
                return Collection([]);

            default:
                throw GraphException.NotFound();
        }
    }

    private static string Recent => Iso(DateTimeOffset.UtcNow.AddHours(-1));

    private static string Soon => Iso(DateTimeOffset.UtcNow.AddDays(20));

    private static string Old => Iso(DateTimeOffset.UtcNow.AddDays(-50));

    private static string Iso(DateTimeOffset date) =>
        date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Collection(IEnumerable<string> items) => $$"""{"value":[{{string.Join(',', items)}}]}""";

    private static readonly (string Key, string Json)[] Groups =
    [
        ("cts", """{"@odata.type":"#microsoft.graph.group","id":"g-laptops","displayName":"CTS Laptops","description":"All Contoso laptops","groupTypes":["DynamicMembership"],"membershipRule":"(device.displayName -startsWith \"CTS-LT\")","securityEnabled":true}"""),
        ("finance", """{"@odata.type":"#microsoft.graph.group","id":"g-finance","displayName":"Finance Users","description":"","groupTypes":[],"securityEnabled":true}"""),
        ("kiosk", """{"@odata.type":"#microsoft.graph.group","id":"g-kiosk","displayName":"Kiosk Devices","description":"","groupTypes":[],"securityEnabled":true}"""),
        ("7zip", """{"@odata.type":"#microsoft.graph.group","id":"g-7zip","displayName":"APP-7-Zip-Install","description":"Install 7-Zip","groupTypes":[],"securityEnabled":true}"""),
    ];

    /// <summary>A–Z by display name, as the Users tab asks for.</summary>
    private static readonly (string Key, string Json)[] People =
    [
        ("alex", """{"id":"u-alex","displayName":"Alex Doe","userPrincipalName":"alex.doe@contoso.com","mail":"alex.doe@contoso.com","jobTitle":"Analyst","department":"Finance"}"""),
        ("casey", """{"id":"u-casey","displayName":"Casey Nguyen","userPrincipalName":"casey.nguyen@contoso.com","mail":"casey.nguyen@contoso.com","jobTitle":"Designer","department":"Marketing"}"""),
        ("jordan", """{"id":"u-jordan","displayName":"Jordan Kim","userPrincipalName":"jordan.kim@contoso.com","mail":"jordan.kim@contoso.com","jobTitle":"Service Desk Analyst","department":"IT"}"""),
        ("morgan", """{"id":"u-morgan","displayName":"Morgan Reyes","userPrincipalName":"morgan.reyes@contoso.com","mail":"morgan.reyes@contoso.com","jobTitle":"Field Engineer","department":"IT"}"""),
        ("priya", """{"id":"u-priya","displayName":"Priya Patel","userPrincipalName":"priya.patel@contoso.com","mail":"priya.patel@contoso.com","jobTitle":"Accountant","department":"Finance"}"""),
        ("sam", """{"id":"u-sam","displayName":"Sam Lee","userPrincipalName":"sam.lee@contoso.com","mail":"sam.lee@contoso.com","jobTitle":"Engineer","department":"IT"}"""),
    ];

    /// <summary>
    /// The rest of the demo fleet, for the Devices and Users tabs. Each one opens to a working
    /// device page: an Entra object, a compliance policy, and (for Windows) i-1's inventory.
    /// </summary>
    private static readonly (string Id, string Name, string Serial, string Upn, string User, string Os, string OsVersion, string Model, string Maker, string Compliance, bool Stale)[] Fleet =
    [
        ("i-4", "CTS-LT-01301", "5CG2301ABC", "jordan.kim@contoso.com", "Jordan Kim", "Windows", "10.0.26100.4652", "EliteBook 840 G11", "HP", "compliant", false),
        ("i-5", "CTS-LT-01322", "5CG2322DEF", "priya.patel@contoso.com", "Priya Patel", "Windows", "10.0.26100.4652", "EliteBook 840 G11", "HP", "compliant", false),
        ("i-6", "CTS-LT-01188", "5CG1188GHJ", "casey.nguyen@contoso.com", "Casey Nguyen", "Windows", "10.0.22631.4037", "EliteBook 840 G9", "HP", "noncompliant", true),
        ("i-7", "CTS-SP-0007", "0F33JK7MN", "morgan.reyes@contoso.com", "Morgan Reyes", "Windows", "10.0.26100.4652", "Surface Pro 11", "Microsoft Corporation", "compliant", false),
        ("i-8", "Alex's iPhone", "G6TZQ1ABC2", "alex.doe@contoso.com", "Alex Doe", "iOS", "18.5", "iPhone 16", "Apple", "compliant", false),
        ("i-9", "CTS-MAC-0003", "C02XK1ABC3", "sam.lee@contoso.com", "Sam Lee", "macOS", "15.5", "MacBook Air", "Apple", "compliant", false),
        ("i-10", "CTS-KIOSK-01", "MXL4051GHJ", "", "", "Windows", "10.0.26100.4652", "EliteDesk 800 G9", "HP", "compliant", false),
    ];

    private static string FleetEntraId(int index) => $"a0000000-0000-4000-8000-{index + 4:000000000000}";

    private static string FleetDevice(int index)
    {
        var device = Fleet[index];
        var user = device.User.Length == 0 ? "null" : $"\"{device.User}\"";
        return $$"""
            {"id":"{{device.Id}}","deviceName":"{{device.Name}}","serialNumber":"{{device.Serial}}","userPrincipalName":"{{device.Upn}}",
             "userDisplayName":{{user}},"lastSyncDateTime":"{{(device.Stale ? Old : Recent)}}","complianceState":"{{device.Compliance}}",
             "operatingSystem":"{{device.Os}}","osVersion":"{{device.OsVersion}}","model":"{{device.Model}}","manufacturer":"{{device.Maker}}",
             "managedDeviceOwnerType":"company","azureADDeviceId":"{{FleetEntraId(index)}}","enrolledDateTime":"2025-09-15T09:00:00Z",
             "isEncrypted":true,"totalStorageSpaceInBytes":510027366400,"freeStorageSpaceInBytes":301989888000}
            """;
    }

    private static string FleetEntra(int index)
    {
        var device = Fleet[index];
        var trust = device.Os == "Windows" ? "AzureAd" : "Workplace";
        return $$"""
            {"id":"e-{{index + 4}}","displayName":"{{device.Name}}","accountEnabled":true,"approximateLastSignInDateTime":"{{(device.Stale ? Old : Recent)}}",
             "trustType":"{{trust}}","operatingSystem":"{{device.Os}}"}
            """;
    }

    private static IEnumerable<string> AllDevices =>
        new[] { Healthy }.Concat(Duplicates).Concat(Enumerable.Range(0, Fleet.Length).Select(FleetDevice));

    /// <summary>Requests about the extra fleet: serial and Entra ID lookups, people's devices, and per-device reads.</summary>
    private static string? FleetResponse(string path, string query)
    {
        if (path == "deviceManagement/managedDevices" && SerialFilter().Match(query) is { Success: true } serial)
        {
            var index = Array.FindIndex(Fleet, device => device.Serial == serial.Groups[1].Value);
            if (index >= 0) return Collection([FleetDevice(index)]);
        }
        if ((path == "deviceManagement/managedDevices" || path == "devices") && EntraIdFilter().Match(query) is { Success: true } entra)
        {
            var index = Enumerable.Range(0, Fleet.Length).FirstOrDefault(i => FleetEntraId(i) == entra.Groups[1].Value, -1);
            if (index >= 0) return Collection([path == "devices" ? FleetEntra(index) : FleetDevice(index)]);
        }
        if (UserDevices().Match(path) is { Success: true } owner)
        {
            var upn = People.Select(person => person.Json).FirstOrDefault(json => json.Contains($"\"id\":\"{owner.Groups[1].Value}\"", StringComparison.Ordinal)) is { } json
                ? UpnField().Match(json).Groups[1].Value
                : "";
            return Collection(AllDevices.Where(record => upn.Length > 0 && record.Contains($"\"userPrincipalName\":\"{upn}\"", StringComparison.Ordinal)));
        }
        if (DeviceRoute().Match(path) is { Success: true } route && Array.FindIndex(Fleet, device => device.Id == route.Groups[1].Value) is var fleetIndex and >= 0)
        {
            var rest = route.Groups[2].Value;
            if (rest == "/deviceCompliancePolicyStates") return Collection([Policy($"{Fleet[fleetIndex].Os} baseline", Fleet[fleetIndex].Compliance == "compliant" ? "compliant" : "nonCompliant", [])]);
            // Inventory: the same sample as CTS-LT-01234, under this device's ID.
            return Respond(GraphRequest.Get($"deviceManagement/managedDevices/i-1{rest}", ParseQuery(query))).Replace("\"id\":\"i-1\"", $"\"id\":\"{Fleet[fleetIndex].Id}\"", StringComparison.Ordinal);
        }
        return null;
    }

    private static IReadOnlyList<(string Name, string Value)> ParseQuery(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(parts => (parts[0], parts.Length > 1 ? parts[1] : ""))
            .ToList();

    private static string Group(string key) => Groups.First(group => group.Key == key).Json;

    private static string Person(string key) => People.First(person => person.Key == key).Json;

    private static string Healthy => $$"""
        {"id":"i-1","deviceName":"CTS-LT-01234","serialNumber":"5CG1234XYZ","userPrincipalName":"alex.doe@contoso.com",
         "userDisplayName":"Alex Doe","lastSyncDateTime":"{{Recent}}","complianceState":"compliant","operatingSystem":"Windows",
         "osVersion":"10.0.26100.4652","model":"EliteBook 840 G10","manufacturer":"HP","managedDeviceOwnerType":"company",
         "azureADDeviceId":"c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b","enrolledDateTime":"2025-03-04T09:15:00Z","isEncrypted":true,
         "totalStorageSpaceInBytes":255013683200,"freeStorageSpaceInBytes":120259084288}
        """;

    private static string[] Duplicates =>
    [
        $$"""
        {"id":"i-2","deviceName":"DESKTOP-7Q2LMNP","serialNumber":"PF2ABCDE","userPrincipalName":"","userDisplayName":null,
         "lastSyncDateTime":"{{Old}}","complianceState":"noncompliant","operatingSystem":"Windows","osVersion":"10.0.22631.4037",
         "model":"ThinkCentre M70q","manufacturer":"LENOVO","managedDeviceOwnerType":"company",
         "azureADDeviceId":"d1b2c3d4-0000-4000-8000-123456789abc","enrolledDateTime":"2024-11-01T10:00:00Z","isEncrypted":false,
         "totalStorageSpaceInBytes":255013683200,"freeStorageSpaceInBytes":9663676416}
        """,
        $$"""
        {"id":"i-3","deviceName":"CTS-DT-00042","serialNumber":"PF2ABCDE","userPrincipalName":"sam.lee@contoso.com",
         "userDisplayName":"Sam Lee","lastSyncDateTime":"{{Recent}}","complianceState":"inGracePeriod","operatingSystem":"Windows",
         "osVersion":"10.0.26100.4652","model":"ThinkCentre M70q","manufacturer":"LENOVO","managedDeviceOwnerType":"company",
         "azureADDeviceId":"00000000-0000-0000-0000-000000000000","enrolledDateTime":"2026-10-01T10:00:00Z","isEncrypted":true}
        """,
    ];

    private static string EntraHealthy => $$"""
        {"id":"e-1","displayName":"CTS-LT-01234","accountEnabled":true,"approximateLastSignInDateTime":"{{Recent}}",
         "trustType":"ServerAd","operatingSystem":"Windows",
         "extensionAttributes":{"extensionAttribute1":"Finance","extensionAttribute2":null,"extensionAttribute3":"Standard laptop",
         "extensionAttribute4":null,"extensionAttribute5":null,"extensionAttribute6":null,"extensionAttribute7":null,"extensionAttribute8":null,
         "extensionAttribute9":null,"extensionAttribute10":"Contoso HQ","extensionAttribute11":null,"extensionAttribute12":null,
         "extensionAttribute13":null,"extensionAttribute14":null,"extensionAttribute15":null} }
        """;

    private static string EntraDisabled => $$"""
        {"id":"e-2","displayName":"DESKTOP-OLDNAME","accountEnabled":false,"approximateLastSignInDateTime":"{{Old}}",
         "trustType":"AzureAd","operatingSystem":"Windows"}
        """;

    private static string Autopilot(string serial, string state)
    {
        var enrolled = state == "enrolled";
        var groupTag = serial.StartsWith("BOX", StringComparison.Ordinal) ? "" : "CTS-Standard";
        return $$"""
            {"id":"ap-{{serial}}","groupTag":"{{groupTag}}","serialNumber":"{{serial}}","manufacturer":"HP","model":"EliteBook 840 G11",
             "enrollmentState":"{{state}}","lastContactedDateTime":"{{(enrolled ? Recent : "0001-01-01T00:00:00Z")}}",
             "userPrincipalName":"{{(enrolled ? "alex.doe@contoso.com" : "")}}"}
            """;
    }

    private static string Policy(string name, string state, IReadOnlyList<(string Setting, string Name, string Value)> failing)
    {
        var settings = failing.Select(setting =>
            $$"""{"setting":"{{setting.Setting}}","settingName":"{{setting.Name}}","state":"nonCompliant","currentValue":"{{setting.Value}}"}""");
        var id = "p-" + name.ToLowerInvariant().Replace(' ', '-');
        return $$"""{"id":"{{id}}","displayName":"{{name}}","state":"{{state}}","settingStates":[{{string.Join(',', settings)}}]}""";
    }

    private static string Assignable(
        string id,
        string name,
        string? type = null,
        string? publisher = null,
        IReadOnlyList<(string Kind, string? Group, string? Intent)>? targets = null)
    {
        var assignments = (targets ?? []).Select(target =>
        {
            var odataType = target.Kind switch
            {
                "allDevices" => "allDevicesAssignmentTarget",
                "allUsers" => "allLicensedUsersAssignmentTarget",
                "exclusion" => "exclusionGroupAssignmentTarget",
                _ => "groupAssignmentTarget",
            };
            var group = target.Group is null ? "" : $",\"groupId\":\"{target.Group}\"";
            var intent = target.Intent is null ? "" : $"\"intent\":\"{target.Intent}\",";
            return $"{{{intent}\"target\":{{\"@odata.type\":\"#microsoft.graph.{odataType}\"{group}}}}}";
        });
        var typeField = type is null ? "" : $"\"@odata.type\":\"#microsoft.graph.{type}\",";
        var publisherField = publisher is null ? "" : $"\"publisher\":\"{publisher}\",";
        return $"{{{typeField}{publisherField}\"id\":\"{id}\",\"displayName\":\"{name}\",\"assignments\":[{string.Join(',', assignments)}]}}";
    }

    [GeneratedRegex(@"startswith\(deviceName,'([^']*)'\)")]
    private static partial Regex NamePrefix();

    [GeneratedRegex("\"deviceName\":\"([^\"]*)\"")]
    private static partial Regex DeviceName();

    [GeneratedRegex("'(BOX[A-Z0-9-]*)'")]
    private static partial Regex BoxSerial();

    [GeneratedRegex("serialNumber eq '([^']*)'")]
    private static partial Regex SerialFilter();

    [GeneratedRegex("(?:azureADDeviceId|deviceId) eq '([^']*)'")]
    private static partial Regex EntraIdFilter();

    [GeneratedRegex("^users/(u-[a-z]+)/managedDevices$")]
    private static partial Regex UserDevices();

    [GeneratedRegex("^deviceManagement/managedDevices/(i-[0-9]+)(/.*)?$")]
    private static partial Regex DeviceRoute();

    [GeneratedRegex("\"userPrincipalName\":\"([^\"]*)\"")]
    private static partial Regex UpnField();

    [GeneratedRegex("\"displayName\":\"([^\"]*)\"")]
    private static partial Regex GroupName();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Slug();

    // ---------- install status ----------
    [GeneratedRegex(@"^users\('[^']*'\)/mobileAppIntentAndStates\('(i-[0-9]+)'\)$")]
    private static partial Regex IntentsRoute();

    [GeneratedRegex(@"ApplicationId eq '([^']*)'")]
    private static partial Regex ReportApp();

    [GeneratedRegex(@"IntuneDeviceId eq '([^']*)'")]
    private static partial Regex ReportDevice();

    /// <summary>
    /// CTS-LT-01234 has a failed VPN (installed but not detected) and a pending Teams; DESKTOP-7Q2LMNP a
    /// failed VPN too (1603). Field Tools is ARM64 only, so it doesn't apply anywhere; the rest are clean.
    /// </summary>
    private static string DemoAppIntents(string deviceId)
    {
        static string App(string id, string name, string intent, string version, string state) =>
            $$"""{"applicationId":"{{id}}","displayName":"{{name}}","mobileAppIntent":"{{intent}}","displayVersion":"{{version}}","installState":"{{state}}","supportedDeviceTypes":[]}""";
        var vpn = deviceId is "i-1" or "i-2" ? "failed" : "installed";
        var teams = deviceId == "i-1" ? "pendingInstall" : "installed";
        string[] apps =
        [
            App("app-portal", "Company Portal", "requiredInstall", "11.2.1495.0", "installed"),
            App("app-m365", "Microsoft 365 Apps for Windows 10 and later", "requiredInstall", "", "installed"),
            App("app-vpn", "Contoso VPN 4.2", "requiredInstall", "4.2.0", vpn),
            App("app-teams", "Microsoft Teams", "requiredInstall", "26225.1806", teams),
            App("app-fieldtools", "Contoso Field Tools 2.1", "requiredInstall", "2.1.0", "notApplicable"),
            App("app-oldvpn", "Old VPN client", "requiredUninstall", "", "notInstalled"),
            App("app-7zip", "7-Zip 25.01", "available", "25.01", "installed"),
            App("app-reader", "Adobe Acrobat Reader", "available", "", "unknown"),
        ];
        return $$"""{"id":"u-demo","managedDeviceIdentifier":"{{deviceId}}","userId":"u-demo","mobileAppList":[{{string.Join(",", apps)}}]}""";
    }

    private const string DesktopsFilter =
        """{"id":"flt-desktops","displayName":"Contoso desktops","rule":"(device.model -startsWith \"ThinkCentre\")","platform":"windows10AndLater"}""";

    private const string StandardReturnCodes =
        """[{"returnCode":0,"type":"success"},{"returnCode":1707,"type":"success"},{"returnCode":3010,"type":"softReboot"},{"returnCode":1641,"type":"hardReboot"},{"returnCode":1618,"type":"retry"}]""";

    /// <summary>An app assignment as beta returns it, with the filter fields.</summary>
    private static string AppTarget(string kind, string? group, string intent, string? filter = null, string filterType = "none")
    {
        var odataType = kind switch
        {
            "allDevices" => "allDevicesAssignmentTarget",
            "allUsers" => "allLicensedUsersAssignmentTarget",
            "exclusion" => "exclusionGroupAssignmentTarget",
            _ => "groupAssignmentTarget",
        };
        var groupField = group is null ? "" : $",\"groupId\":\"{group}\"";
        var filterId = filter is null ? "null" : $"\"{filter}\"";
        return $$"""{"id":"{{group ?? kind}}_{{intent}}","intent":"{{intent}}","source":"direct","target":{"@odata.type":"#microsoft.graph.{{odataType}}","deviceAndAppManagementAssignmentFilterId":{{filterId}},"deviceAndAppManagementAssignmentFilterType":"{{filterType}}"{{groupField}}""" + "}}";
    }

    /// <summary>The demo apps' Intune settings, for "Why didn't it install?".</summary>
    private static string DemoApp(string path, string query)
    {
        var id = path["deviceAppManagement/mobileApps/".Length..];
        (string Type, string Name, string Publisher, string[] Assignments, string Relationships, string? Win32) app = id switch
        {
            "app-portal" => ("winGetApp", "Company Portal", "Microsoft Corporation", [AppTarget("allUsers", null, "required")], "", null),
            "app-m365" => ("officeSuiteApp", "Microsoft 365 Apps for Windows 10 and later", "Microsoft", [AppTarget("allDevices", null, "required")], "", null),
            "app-vpn" => ("win32LobApp", "Contoso VPN 4.2", "Contoso IT",
                [AppTarget("group", "g-laptops", "required"), AppTarget("allDevices", null, "required", "flt-desktops", "include")], "",
                $$"""
                "displayVersion":"4.2.0","allowedArchitectures":"x64,arm64","applicableArchitectures":"none","minimumSupportedWindowsRelease":"Windows11_23H2",
                "minimumFreeDiskSpaceInMB":500,"minimumMemoryInMB":null,"minimumNumberOfProcessors":null,"minimumCpuSpeedInMHz":null,
                "rules":[{"@odata.type":"#microsoft.graph.win32LobAppFileSystemRule","ruleType":"detection","path":"C:\\Program Files\\Contoso\\VPN","fileOrFolderName":"vpnclient.exe","check32BitOn64System":false,"operationType":"version","operator":"greaterThanOrEqual","comparisonValue":"4.2.0"}],
                "returnCodes":{{StandardReturnCodes}},"installExperience":{"runAsAccount":"system","maxRunTimeInMinutes":60,"deviceRestartBehavior":"basedOnReturnCode"},
                "installCommandLine":"msiexec /i \"ContosoVPN-4.2.0.msi\" /qn","uninstallCommandLine":"msiexec /x \"{6F0A7B52-3C1D-4E8F-9A2B-5C7D8E9F0A1B}\" /qn","setupFilePath":"ContosoVPN-4.2.0.msi"
                """),
            "app-teams" => ("win32LobApp", "Microsoft Teams", "Microsoft Corporation", [AppTarget("allUsers", null, "required")],
                """{"@odata.type":"#microsoft.graph.mobileAppDependency","targetId":"app-webview2","targetDisplayName":"Microsoft Edge WebView2 Runtime","targetDisplayVersion":"141.0","targetType":"child","dependencyType":"autoInstall"}""",
                $$"""
                "displayVersion":"26225.1806","allowedArchitectures":"x64,arm64","applicableArchitectures":"none","minimumSupportedWindowsRelease":"1607","minimumFreeDiskSpaceInMB":1024,
                "rules":[{"@odata.type":"#microsoft.graph.win32LobAppPowerShellScriptRule","ruleType":"detection","enforceSignatureCheck":false,"runAs32Bit":false,"operationType":"notConfigured","operator":"notConfigured","comparisonValue":null}],
                "returnCodes":{{StandardReturnCodes}},"installExperience":{"runAsAccount":"system"},
                "installCommandLine":"teamsbootstrapper.exe -p","uninstallCommandLine":"teamsbootstrapper.exe -x","setupFilePath":"teamsbootstrapper.exe"
                """),
            "app-fieldtools" => ("win32LobApp", "Contoso Field Tools 2.1", "Contoso IT", [AppTarget("group", "g-laptops", "required")], "",
                $$"""
                "displayVersion":"2.1.0","allowedArchitectures":"arm64","applicableArchitectures":"none","minimumSupportedWindowsRelease":"Windows11_24H2","minimumFreeDiskSpaceInMB":200,
                "rules":[
                 {"@odata.type":"#microsoft.graph.win32LobAppFileSystemRule","ruleType":"requirement","path":"C:\\ProgramData\\Contoso","fileOrFolderName":"field-device.tag","check32BitOn64System":false,"operationType":"exists","operator":"notConfigured","comparisonValue":null},
                 {"@odata.type":"#microsoft.graph.win32LobAppRegistryRule","ruleType":"detection","keyPath":"HKEY_LOCAL_MACHINE\\SOFTWARE\\Contoso\\FieldTools","valueName":"Version","check32BitOn64System":false,"operationType":"version","operator":"greaterThanOrEqual","comparisonValue":"2.1.0"}],
                "returnCodes":{{StandardReturnCodes}},"installExperience":{"runAsAccount":"system"},
                "installCommandLine":"FieldToolsSetup.exe /S","uninstallCommandLine":"FieldToolsSetup.exe /uninstall /S","setupFilePath":"FieldToolsSetup.exe"
                """),
            "app-oldvpn" => ("win32LobApp", "Old VPN client", "Contoso IT", [AppTarget("group", "g-finance", "uninstall")], "",
                """
                "displayVersion":"3.9.2","allowedArchitectures":"x64","applicableArchitectures":"none","minimumSupportedWindowsRelease":"1607",
                "rules":[{"@odata.type":"#microsoft.graph.win32LobAppProductCodeRule","ruleType":"detection","productCode":"{2B1C0D9E-8F7A-4B6C-5D4E-3F2A1B0C9D8E}","productVersionOperator":"notConfigured","productVersion":null}],
                "installExperience":{"runAsAccount":"system"},"installCommandLine":"msiexec /i \"OldVPN.msi\" /qn","uninstallCommandLine":"msiexec /x \"{2B1C0D9E-8F7A-4B6C-5D4E-3F2A1B0C9D8E}\" /qn","setupFilePath":"OldVPN.msi"
                """),
            "app-7zip" => ("winGetApp", "7-Zip 25.01", "Igor Pavlov", [AppTarget("group", "g-laptops", "available")], "", null),
            "app-reader" => ("win32LobApp", "Adobe Acrobat Reader", "Adobe", [AppTarget("group", "g-finance", "available")], "",
                """
                "displayVersion":"25.001.20756","allowedArchitectures":"x64","applicableArchitectures":"none","minimumSupportedWindowsRelease":"1607",
                "rules":[{"@odata.type":"#microsoft.graph.win32LobAppRegistryRule","ruleType":"detection","keyPath":"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{AC76BA86-1033-1033-7760-BC15014EA700}","valueName":"","check32BitOn64System":false,"operationType":"exists","operator":"notConfigured","comparisonValue":null}],
                "installExperience":{"runAsAccount":"system"},"installCommandLine":"setup.exe /sAll /rs","uninstallCommandLine":"msiexec /x {AC76BA86-1033-1033-7760-BC15014EA700} /qn","setupFilePath":"setup.exe"
                """),
            _ => throw GraphException.NotFound(),
        };
        // The second read names Win32 fields; Graph refuses that for any other type.
        if (query.Contains("microsoft.graph.win32LobApp/", StringComparison.Ordinal))
        {
            return app.Win32 is { } win32 ? $$"""{"@odata.type":"#microsoft.graph.win32LobApp","id":"{{id}}",{{win32}}}""" : throw GraphException.Decoding("not a Win32 app");
        }
        return $$"""{"@odata.type":"#microsoft.graph.{{app.Type}}","id":"{{id}}","displayName":"{{app.Name}}","publisher":"{{app.Publisher}}","assignments":[{{string.Join(",", app.Assignments)}}],"relationships":[{{app.Relationships}}]}""";
    }

    private static string DemoReport(string path, string body)
    {
        static string Cell(object? value) => value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!,
        };
        static string Table((string Name, string Type)[] schema, object?[][] rows) =>
            $$"""{"SessionId":"demo","TotalRowCount":{{rows.Length}},"Schema":[{{string.Join(",", schema.Select(column => $$"""{"Column":"{{column.Name}}","PropertyType":"{{column.Type}}"}"""))}}],"Values":[{{string.Join(",", rows.Select(row => "[" + string.Join(",", row.Select(Cell)) + "]"))}}]}""";

        if (path.EndsWith("retrieveDeviceAppInstallationStatusReport", StringComparison.Ordinal))
        {
            (string, string)[] schema = [("DeviceId", "String"), ("ApplicationId", "String"), ("DeviceName", "String"), ("ErrorCode", "Int32"), ("InstallState", "Int32"),
                ("LastModifiedDateTime", "DateTime"), ("HexErrorCode", "String"), ("AppInstallState_loc", "String"), ("AppInstallStateDetails_loc", "String")];
            var app = ReportApp().Match(body).Groups[1].Value;
            var when = DateTimeOffset.UtcNow.AddHours(-3).ToString("M/d/yyyy h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
            object?[][] rows = app != "app-vpn" ? [] :
            [
                ["i-1", "app-vpn", "CTS-LT-01234", -2016345060, 2, when, "0x87D1041C", "Failed", "The application was not detected after installation completed successfully"],
                ["i-2", "app-vpn", "DESKTOP-7Q2LMNP", -2147023293, 2, when, "0x80070643", "Failed", ""],
            ];
            return Table(schema, rows);
        }
        if (path.EndsWith("getConfigurationPoliciesReportForDevice", StringComparison.Ordinal))
        {
            (string, string)[] schema = [("IntuneDeviceId", "String"), ("PolicyBaseTypeName", "String"), ("PolicyId", "String"), ("PolicyName", "String"), ("PolicyStatus", "Int32"),
                ("PspdpuLastModifiedTimeUtc", "DateTime"), ("UnifiedPolicyType", "String"), ("UnifiedPolicyType_loc", "String"), ("UPN", "String"), ("UserId", "String")];
            var device = ReportDevice().Match(body).Groups[1].Value;
            var when = DateTimeOffset.UtcNow.AddHours(-5).ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            var bitlocker = device == "i-2" ? 5 : 2;
            var edge = device == "i-1" ? 6 : 2;
            object?[][] rows =
            [
                [device, "DeviceManagementConfigurationPolicy", "cfg-baseline", "Windows security baseline", 2, when, "SettingsCatalog", "Settings Catalog", "", NoUserId],
                [device, "DeviceManagementConfigurationPolicy", "cfg-baseline", "Windows security baseline", 2, when, "SettingsCatalog", "Settings Catalog", "alex.doe@contoso.com", "u-alex"],
                [device, "DeviceManagementConfigurationPolicy", "cfg-bitlocker", "BitLocker", bitlocker, when, "Bitlocker", "BitLocker", "", NoUserId],
                [device, "DeviceConfigurationAdmxPolicy", "cfg-edge", "Microsoft Edge settings", edge, when, "AdministrativeTemplates", "Administrative Templates", "", NoUserId],
                [device, "Microsoft.Management.Services.Api.DeviceConfiguration", "cfg-wifi", "Wi-Fi - Contoso HQ", 2, when, "WiFi", "Wi-Fi", "", NoUserId],
                [device, "Microsoft.Management.Services.Api.DeviceConfiguration", "cfg-updates", "Windows Update ring", 2, when, "SoftwareUpdates", "Software Updates", "", NoUserId],
            ];
            return Table(schema, rows);
        }
        throw GraphException.NotFound();
    }

    private const string NoUserId = "00000000-0000-0000-0000-000000000000";

    [GeneratedRegex("userId eq '([^']*)'")]
    private static partial Regex SignInUser();

    // ---------- sign-ins ("why was I blocked?") ----------
    private const string CompliantPolicy = "Require compliant device for Microsoft 365";
    private const string MfaPolicy = "Require MFA for all users";
    private const string LegacyPolicy = "Block legacy authentication";
    private const string CountryPolicy = "Block sign-ins from outside the US (report-only)";

    private static (string Name, string Result, string[] Grants) Pol(string name, string result, params string[] grants) => (name, result, grants);

    private static string Policies((string Name, string Result, string[] Grants)[] policies) =>
        "[" + string.Join(",", policies.Select(policy =>
            $$"""{"id":"ca-{{Slug().Replace(policy.Name.ToLowerInvariant(), "-").Trim('-')}}","displayName":"{{policy.Name}}","enforcedGrantControls":[{{string.Join(",", policy.Grants.Select(grant => $"\"{grant}\""))}}],"enforcedSessionControls":[],"result":"{{policy.Result}}"}""")) + "]";

    private static string Entry(string id, int minutesAgo, string user, string upn, string app, string client, int error, string reason, string ca,
        (string Name, string Result, string[] Grants)[] policies, string deviceId, string deviceName, string os, string browser, bool? compliant, bool? managed, string trust,
        string city, string state, string country, string ip)
    {
        var when = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        static string Bool(bool? value) => value is { } b ? (b ? "true" : "false") : "null";
        return $$"""
            {"id":"{{id}}","createdDateTime":"{{when}}","userDisplayName":"{{user}}","userPrincipalName":"{{upn}}","appDisplayName":"{{app}}","resourceDisplayName":"{{app}}",
             "clientAppUsed":"{{client}}","ipAddress":"{{ip}}","correlationId":"{{id}}-c0","conditionalAccessStatus":"{{ca}}","isInteractive":true,
             "status":{"errorCode":{{error}},"failureReason":{{(reason.Length == 0 ? "null" : $"\"{reason}\"")}},"additionalDetails":null},
             "deviceDetail":{"deviceId":"{{deviceId}}","displayName":{{(deviceName.Length == 0 ? "null" : $"\"{deviceName}\"")}},"operatingSystem":"{{os}}","browser":"{{browser}}","isCompliant":{{Bool(compliant)}},"isManaged":{{Bool(managed)}},"trustType":{{(trust.Length == 0 ? "null" : $"\"{trust}\"")}}},
             "location":{"city":"{{city}}","state":"{{state}}","countryOrRegion":"{{country}}"},
             "appliedConditionalAccessPolicies":{{Policies(policies)}}}
            """;
    }

    /// <summary>Casey is blocked by compliance, Jordan by legacy auth, Alex mistypes then signs in, Priya trips a report-only policy.</summary>
    private static IEnumerable<string> DemoSignIns(string userId)
    {
        const string redmond = "Redmond", washington = "Washington", us = "US";
        switch (userId)
        {
            case "u-casey":
                var casey = FleetEntraId(2);
                (string, string, string[])[] blocked = [Pol(CompliantPolicy, "failure", "RequireCompliantDevice"), Pol(MfaPolicy, "success", "Mfa"), Pol(LegacyPolicy, "notApplied"), Pol(CountryPolicy, "reportOnlyNotApplied", "Block")];
                yield return Entry("si-casey-1", 38, "Casey Nguyen", "casey.nguyen@contoso.com", "Microsoft Teams", "Mobile Apps and Desktop clients", 53000,
                    "Device is not in required device state: compliant. Conditional Access policy requires a compliant device, and the device is not compliant.", "failure",
                    blocked, casey, "CTS-LT-01188", "Windows10", "", false, true, "Azure AD joined", redmond, washington, us, "203.0.113.24");
                yield return Entry("si-casey-2", 44, "Casey Nguyen", "casey.nguyen@contoso.com", "Microsoft Outlook", "Mobile Apps and Desktop clients", 53000,
                    "Device is not in required device state: compliant. Conditional Access policy requires a compliant device, and the device is not compliant.", "failure",
                    blocked, casey, "CTS-LT-01188", "Windows10", "", false, true, "Azure AD joined", redmond, washington, us, "203.0.113.24");
                yield return Entry("si-casey-3", 60 * 26, "Casey Nguyen", "casey.nguyen@contoso.com", "OfficeHome", "Browser", 0, "", "success",
                    [Pol(CompliantPolicy, "success", "RequireCompliantDevice"), Pol(MfaPolicy, "success", "Mfa"), Pol(LegacyPolicy, "notApplied")],
                    casey, "CTS-LT-01188", "Windows10", "Edge 141.0", true, true, "Azure AD joined", redmond, washington, us, "203.0.113.24");
                break;
            case "u-jordan":
                yield return Entry("si-jordan-1", 95, "Jordan Kim", "jordan.kim@contoso.com", "Office 365 Exchange Online", "Exchange ActiveSync", 53003,
                    "Access has been blocked by Conditional Access policies. The access policy does not allow token issuance.", "failure",
                    [Pol(LegacyPolicy, "failure", "Block"), Pol(MfaPolicy, "notApplied", "Mfa"), Pol(CompliantPolicy, "notApplied")],
                    "", "", "Ios", "", null, null, "", redmond, washington, us, "198.51.100.7");
                yield return Entry("si-jordan-2", 180, "Jordan Kim", "jordan.kim@contoso.com", "Microsoft Teams", "Browser", 0, "", "success",
                    [Pol(MfaPolicy, "success", "Mfa"), Pol(CompliantPolicy, "success", "RequireCompliantDevice")],
                    FleetEntraId(0), "CTS-LT-01301", "Windows10", "Edge 141.0", true, true, "Azure AD joined", redmond, washington, us, "203.0.113.24");
                break;
            case "u-alex":
                yield return Entry("si-alex-3", 24, "Alex Doe", "alex.doe@contoso.com", "Microsoft Teams", "Mobile Apps and Desktop clients", 0, "", "success",
                    [Pol(CompliantPolicy, "success", "RequireCompliantDevice"), Pol(MfaPolicy, "success", "Mfa"), Pol(CountryPolicy, "reportOnlySuccess", "Block")],
                    "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b", "CTS-LT-01234", "Windows10", "", true, true, "Hybrid Azure AD joined", redmond, washington, us, "203.0.113.24");
                yield return Entry("si-alex-2", 26, "Alex Doe", "alex.doe@contoso.com", "Microsoft Teams", "Mobile Apps and Desktop clients", 50074,
                    "Strong Authentication is required.", "success",
                    [Pol(CompliantPolicy, "success", "RequireCompliantDevice"), Pol(MfaPolicy, "success", "Mfa")],
                    "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b", "CTS-LT-01234", "Windows10", "", true, true, "Hybrid Azure AD joined", redmond, washington, us, "203.0.113.24");
                yield return Entry("si-alex-1", 29, "Alex Doe", "alex.doe@contoso.com", "Microsoft Teams", "Mobile Apps and Desktop clients", 50126,
                    "Error validating credentials due to invalid username or password.", "notApplied", [],
                    "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b", "CTS-LT-01234", "Windows10", "", true, true, "Hybrid Azure AD joined", redmond, washington, us, "203.0.113.24");
                break;
            case "u-priya":
                yield return Entry("si-priya-1", 300, "Priya Patel", "priya.patel@contoso.com", "OfficeHome", "Browser", 0, "", "success",
                    [Pol(MfaPolicy, "success", "Mfa"), Pol(CountryPolicy, "reportOnlyFailure", "Block"), Pol(CompliantPolicy, "notApplied")],
                    "", "", "Windows10", "Chrome 141.0", null, null, "", "Toronto", "Ontario", "CA", "192.0.2.55");
                break;
            default:
                if (userId.Length == 0) yield break;
                yield return Entry($"si-{userId}-1", 120, "", "", "Microsoft Teams", "Mobile Apps and Desktop clients", 0, "", "success",
                    [Pol(MfaPolicy, "success", "Mfa"), Pol(CompliantPolicy, "success", "RequireCompliantDevice")],
                    "", "", "Windows10", "", true, true, "Azure AD joined", redmond, washington, us, "203.0.113.24");
                break;
        }
    }

    /// <summary>The term in <c>$search="displayName:term"</c>.</summary>
    [GeneratedRegex("\\$search=\"[a-zA-Z]+:([^\"]*)\"")]
    private static partial Regex SearchedTerm();

    [GeneratedRegex("\"(?:displayName|userPrincipalName|mail)\":\"([^\"]*)\"")]
    private static partial Regex SearchableField();

    /// <summary>Whether a name, UPN or email has a word starting with the term (Graph's $search tokenises on spaces and punctuation).</summary>
    private static bool StartsAWord(string json, string term) =>
        SearchableField().Matches(json).Any(field =>
            field.Groups[1].Value.StartsWith(term, StringComparison.OrdinalIgnoreCase)
            || field.Groups[1].Value.Split([' ', '.', '@', '-', '_'], StringSplitOptions.RemoveEmptyEntries).Any(word => word.StartsWith(term, StringComparison.OrdinalIgnoreCase)));
}
