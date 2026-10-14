namespace FieldKit.Core.Domain;

/// <summary>A BitLocker recovery key. Shown only after Windows Hello; never stored or logged.</summary>
public sealed record BitLockerRecoveryKey(string Id, string Key, DateTimeOffset? CreatedAt, string Volume)
{
    /// <summary>Graph's <c>volumeType</c> arrives as a number ("1") or a name ("operatingSystemVolume").</summary>
    public static string VolumeName(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "1" or "operatingsystemvolume" => "OS drive",
            "2" or "fixeddatavolume" => "Fixed data drive",
            "3" or "removabledatavolume" => "Removable drive",
            _ => "Drive",
        };

    /// <summary>Leaves the key out, so logging the record can't leak it.</summary>
    public override string ToString() => $"BitLockerRecoveryKey {{ Id = {Id}, Volume = {Volume} }}";
}
