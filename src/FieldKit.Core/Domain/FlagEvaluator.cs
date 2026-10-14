using System.Text.RegularExpressions;

namespace FieldKit.Core.Domain;

/// <summary>
/// Applies the phase 1 flag rules (ports <c>EvaluateFlags.swift</c>). Lookups that are still
/// loading or failed (<see cref="LookupState.Unavailable"/>) never produce flags, so a partial
/// result never claims a device is missing.
/// </summary>
public static class FlagEvaluator
{
    /// <param name="selectedIntuneId">
    /// Which record to evaluate when a serial has several Intune records; defaults to the first.
    /// </param>
    public static IReadOnlyList<Flag> Evaluate(
        Lookup<IReadOnlyList<IntuneDevice>> intune,
        Lookup<EntraDevice> entra,
        Lookup<IReadOnlyList<AutopilotDevice>> autopilot,
        AuditSettings settings,
        string? selectedIntuneId = null,
        DateTimeOffset? now = null)
    {
        var flags = new List<Flag>();
        IntuneDevice? device = null;

        switch (intune.State)
        {
            case LookupState.Found when intune.Value is { Count: > 0 } records:
                var subject = records.FirstOrDefault(record => record.Id == selectedIntuneId) ?? records[0];
                device = subject;
                flags.AddRange(IntuneFlags(subject, settings, now ?? DateTimeOffset.Now));
                if (records.Count > 1)
                {
                    flags.Add(new Flag(FlagKind.DuplicateIntuneRecords, FlagSeverity.Warning,
                        $"{records.Count} Intune records share this serial."));
                }
                if (subject.HasEntraDeviceId && entra.State == LookupState.NotFound)
                {
                    flags.Add(new Flag(FlagKind.NoEntraObject, FlagSeverity.Critical,
                        "Intune points to an Entra device that doesn't exist."));
                }
                break;

            case LookupState.Found or LookupState.NotFound:
                switch (autopilot.State)
                {
                    case LookupState.Found when autopilot.Value is { Count: > 0 }:
                        flags.Add(new Flag(FlagKind.NotInIntune, FlagSeverity.Info,
                            "Registered in Autopilot, not enrolled in Intune."));
                        break;
                    case LookupState.Found or LookupState.NotFound:
                        flags.Add(new Flag(FlagKind.UnknownDevice, FlagSeverity.Warning,
                            "Not found in Intune or Autopilot."));
                        break;
                }
                break;
        }

        if (entra.State == LookupState.Found && entra.Value is { } entraDevice)
        {
            if (!entraDevice.AccountEnabled)
            {
                flags.Add(new Flag(FlagKind.EntraDisabled, FlagSeverity.Critical,
                    "The Entra device object is disabled."));
            }
            if (device is not null
                && !string.Equals(device.DeviceName, entraDevice.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                flags.Add(new Flag(FlagKind.NameMismatch, FlagSeverity.Warning,
                    $"Intune \"{device.DeviceName}\" vs Entra \"{entraDevice.DisplayName}\"."));
            }
        }

        return flags
            .OrderByDescending(flag => flag.Severity)
            .ThenBy(flag => flag.Kind)
            .ToList();
    }

    private static List<Flag> IntuneFlags(IntuneDevice device, AuditSettings settings, DateTimeOffset now)
    {
        var flags = new List<Flag>();

        var threshold = settings.StaleThresholdDays;
        if (device.LastSyncDateTime is { } lastSync)
        {
            var days = (int)((now - lastSync).TotalSeconds / 86_400);
            if (days >= threshold)
            {
                flags.Add(new Flag(FlagKind.Stale, FlagSeverity.Warning,
                    $"Last synced {days} days ago (threshold {threshold})."));
            }
        }
        else
        {
            flags.Add(new Flag(FlagKind.Stale, FlagSeverity.Warning, "No sync recorded."));
        }

        if (string.IsNullOrWhiteSpace(device.UserPrincipalName))
        {
            flags.Add(new Flag(FlagKind.NoPrimaryUser, FlagSeverity.Info, "No primary user assigned."));
        }

        if (device.ComplianceState.Kind != ComplianceStateKind.Compliant)
        {
            // Grace period is expected to resolve on its own, so it informs rather than warns.
            var severity = device.ComplianceState.Kind == ComplianceStateKind.InGracePeriod
                ? FlagSeverity.Info
                : FlagSeverity.Warning;
            flags.Add(new Flag(FlagKind.Noncompliant, severity,
                $"Compliance state: {device.ComplianceState.DisplayName}."));
        }

        var pattern = settings.NamingConventionRegex;
        if (pattern.Length > 0 && AuditSettings.CompileNamingRegex(pattern) is { } regex && !Matches(regex, device.DeviceName))
        {
            flags.Add(new Flag(FlagKind.NameConvention, FlagSeverity.Warning,
                $"\"{device.DeviceName}\" doesn't match the naming convention."));
        }

        return flags;
    }

    /// <summary>A pattern too slow to evaluate is treated as matching, so it can't raise a false flag.</summary>
    private static bool Matches(Regex regex, string name)
    {
        try
        {
            return regex.IsMatch(name);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }
}
