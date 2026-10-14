namespace FieldKit.Core.Domain;

/// <summary>Whether this PC's firmware serial is one FieldKit can look up (the This PC button).</summary>
public static class LocalDeviceSerial
{
    /// <summary>Values firmware reports when the maker never set a serial. Typical of VMs and white-box PCs.</summary>
    private static readonly HashSet<string> Placeholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "To be filled by O.E.M.", "To Be Filled By O.E.M.", "Default string", "System Serial Number",
        "Chassis Serial Number", "Not Specified", "Not Applicable", "None", "N/A", "OEM", "O.E.M.",
        "Default", "Invalid", "Unknown", "SerialNumber", "123456789", "0123456789", "1234567890",
    };

    /// <summary>The normalised serial, or null when it's empty, a placeholder or all zeros.</summary>
    public static string? Usable(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (Placeholders.Contains(trimmed)) return null;
        if (trimmed.All(character => character is '0' or '-' or '.' or ' ')) return null;
        var serial = SerialNormalizer.Normalize(trimmed);
        return serial is null || Placeholders.Contains(serial) ? null : serial;
    }
}
