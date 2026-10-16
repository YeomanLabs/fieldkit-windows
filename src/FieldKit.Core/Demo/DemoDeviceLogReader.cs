using System.Globalization;
using System.Text;
using FieldKit.Core.Domain;
using FieldKit.Core.Services;

namespace FieldKit.Core.Demo;

/// <summary>
/// Demo mode's device logs, in the Intune Management Extension's own format, whatever file is
/// picked. A file named after DESKTOP-7Q2LMNP holds its log (the VPN failing with 1603); anything
/// else holds CTS-LT-01234's (the VPN installing with exit code 0, then not being detected).
/// Nothing is read from disk.
/// </summary>
public sealed class DemoDeviceLogReader : IDeviceLogReader
{
    public Task<DeviceLogFiles> FromFilesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var source = string.Join(", ", paths.Select(Path.GetFileName));
        var desktop = paths.Any(path => path.Contains("DESKTOP-7Q2LMNP", StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(new DeviceLogFiles(source, [new DeviceLogFile("AppWorkload.log", desktop ? Desktop() : Laptop())]));
    }

    /// <summary>CTS-LT-01234: the VPN's installer exits 0, then detection still says not detected.</summary>
    private static string Laptop()
    {
        var log = new Log(new DateTime(2026, 10, 16, 6, 12, 3));
        log.Line("[Win32App][V3Processor] Processing subgraph with app ids: app-vpn");
        log.Line("[Win32App][ApplicabilityActionHandler] Applicability check for policy with id: app-vpn resulted in action status: Success and applicability state: Applicable.");
        log.Line("[Win32App][DetectionActionHandler] Detection for policy with id: app-vpn resulted in action status: Success and detection state: NotDetected.");
        log.Line("[Win32App][ExecutionActionHandler] Handler invoked with execution type: Install for policy with id: app-vpn and version: 1.");
        log.Line("[ProcessMonitor] Process got lpExitCode: 0, lastWin32Error: 0");
        log.Line("[Win32App][ExecutionActionHandler] Handler completed with action status: Success.");
        log.Line("[Win32App][DetectionActionHandler] Detection for policy with id: app-vpn resulted in action status: Success and detection state: NotDetected.");
        log.Line("""[Win32App][ReportingManager] App with id: app-vpn and prior AppAuthority: V3 has been loaded and reporting state initialized. ReportingState: {"ApplicationId":"app-vpn","ResultantAppState":2,"DetectionState":2,"ApplicabilityState":0,"EnforcementState":5000,"EnforcementErrorCode":-2016345060,"DetectedIdentityVersion":null}""");
        log.Line("[Win32App][V3Processor] Done processing subgraph.");
        return log.Text;
    }

    /// <summary>DESKTOP-7Q2LMNP: the VPN's installer fails with 1603.</summary>
    private static string Desktop()
    {
        var log = new Log(new DateTime(2026, 8, 27, 14, 40, 11));
        log.Line("[Win32App][V3Processor] Processing subgraph with app ids: app-vpn");
        log.Line("[Win32App][ApplicabilityActionHandler] Applicability check for policy with id: app-vpn resulted in action status: Success and applicability state: Applicable.");
        log.Line("[Win32App][DetectionActionHandler] Detection for policy with id: app-vpn resulted in action status: Success and detection state: NotDetected.");
        log.Line("[Win32App][ExecutionActionHandler] Handler invoked with execution type: Install for policy with id: app-vpn and version: 1.");
        log.Line("[ProcessMonitor] Process got lpExitCode: 1603, lastWin32Error: 0");
        log.Line("[Win32App][ExecutionActionHandler] Admin did NOT set mapping for lpExitCode: 1603 of app: app-vpn", severity: 2);
        log.Line("[Win32App][ExecutionActionHandler] Handler completed with action status: Failed.");
        log.Line("""[Win32App][ReportingManager] Sending status to company portal based on report: {"ApplicationId":"app-vpn","ResultantAppState":2,"EnforcementState":5000,"EnforcementErrorCode":-2147023293,"DetectedIdentityVersion":null}""");
        log.Line("[Win32App][V3Processor] Done processing subgraph.");
        return log.Text;
    }

    /// <summary>Writes CMTrace lines a second apart, on one thread.</summary>
    private sealed class Log(DateTime start)
    {
        private readonly StringBuilder _text = new();
        private DateTime _at = start;

        public string Text => _text.ToString();

        public void Line(string message, int severity = 1)
        {
            var time = _at.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
            var date = _at.ToString("M-d-yyyy", CultureInfo.InvariantCulture);
            _text.Append($"<![LOG[{message}]LOG]!><time=\"{time}\" date=\"{date}\" component=\"AppWorkload\" context=\"\" type=\"{severity}\" thread=\"14\" file=\"\">\r\n");
            _at = _at.AddSeconds(1);
        }
    }
}
