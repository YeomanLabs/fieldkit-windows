namespace FieldKit.Core.Domain;

/// <summary>How full a disk is, for colouring <see cref="StorageUsage"/>.</summary>
public enum StorageLevel
{
    Ok,
    Low,
    Critical,
}
