namespace FieldKit.Core.Persistence;

/// <summary>Where a scanned device's lookup is (ports <c>ScanRecord.Status</c>; stored by name).</summary>
public enum ScanStatus
{
    Pending,
    Done,
    Failed,
}
