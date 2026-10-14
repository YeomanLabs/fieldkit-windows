using System.Globalization;

namespace FieldKit.Core.Domain;

/// <summary>Free and total disk space, with a level for colouring.</summary>
public sealed record StorageUsage
{
    /// <summary>Below this share free, space is low; below <see cref="CriticalShare"/>, critical.</summary>
    public const double LowShare = 0.15;
    public const double CriticalShare = 0.05;

    private static readonly string[] Units = ["bytes", "KB", "MB", "GB", "TB", "PB"];

    private StorageUsage(long totalBytes, long freeBytes)
    {
        TotalBytes = totalBytes;
        FreeBytes = Math.Min(freeBytes, totalBytes);
    }

    public long TotalBytes { get; }

    public long FreeBytes { get; }

    /// <summary>Null when Intune hasn't reported storage (zeros or missing).</summary>
    public static StorageUsage? Create(long? totalBytes, long? freeBytes) =>
        totalBytes is > 0 && freeBytes is >= 0 ? new StorageUsage(totalBytes.Value, freeBytes.Value) : null;

    public double FreeShare => (double)FreeBytes / TotalBytes;

    public double UsedShare => 1 - FreeShare;

    public StorageLevel Level =>
        FreeShare < CriticalShare ? StorageLevel.Critical
        : FreeShare < LowShare ? StorageLevel.Low
        : StorageLevel.Ok;

    /// <summary>"112 GB free of 237 GB"</summary>
    public string Summary => $"{Format(FreeBytes)} free of {Format(TotalBytes)}";

    /// <summary>Like File Explorer: 1024-based units and three significant digits, truncated.</summary>
    public static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} bytes";
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var decimals = value >= 100 ? 0 : value >= 10 ? 1 : 2;
        var factor = Math.Pow(10, decimals);
        var truncated = Math.Floor(value * factor) / factor;
        return truncated.ToString("F" + decimals, CultureInfo.CurrentCulture) + " " + Units[unit];
    }
}
