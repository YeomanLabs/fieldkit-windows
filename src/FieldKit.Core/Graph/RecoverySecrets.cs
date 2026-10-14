using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// LAPS passwords and BitLocker recovery keys. Both APIs require the ocp-client headers.
/// Results are returned to the caller only; nothing here logs or stores them.
/// </summary>
public sealed class RecoverySecrets(IGraphClient client)
{
    public static readonly IReadOnlyDictionary<string, string> ClientHeaders = new Dictionary<string, string>
    {
        ["ocp-client-name"] = "FieldKit",
        ["ocp-client-version"] = "1.0",
    };

    /// <summary>Enough for every volume on a normal device without fetching a long history.</summary>
    public const int MaxBitLockerKeys = 6;

    /// <summary>The newest LAPS credential for an Entra device, or null if none is backed up.</summary>
    public async Task<LocalAdminCredential?> LocalAdminCredentialAsync(string entraDeviceId, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get($"directory/deviceLocalCredentials/{entraDeviceId}",
            [("$select", "id,deviceName,lastBackupDateTime,refreshDateTime,credentials")], ClientHeaders);
        try
        {
            return (await client.GetAsync<DeviceLocalCredentialInfoDto>(request, cancellationToken).ConfigureAwait(false)).Newest();
        }
        catch (GraphException error) when (IsAbsent(error))
        {
            return null;
        }
    }

    /// <summary>
    /// How Graph says there's nothing for this device: 404, or HTTP 200 with an empty body
    /// (the LAPS API did this for a device with no password on a real tenant, 2026-10-14).
    /// </summary>
    private static bool IsAbsent(GraphException error) => error.Kind == GraphErrorKind.NotFound || error.IsEmptyResponse;

    /// <summary>
    /// Recovery keys for a device, newest first, each fetched with its key. Keys are filed
    /// under the Entra device ID; the object ID is tried too in case a tenant filed them there.
    /// </summary>
    public async Task<BitLockerKeyLookup> BitLockerKeysAsync(string entraDeviceId, string? entraObjectId, CancellationToken cancellationToken = default)
    {
        var idsTried = new List<string>();
        IReadOnlyList<BitLockerRecoveryKeyDto> entries = [];
        foreach (var id in new[] { entraDeviceId, entraObjectId })
        {
            if (id is null || idsTried.Contains(id)) continue;
            idsTried.Add(id);
            var list = GraphRequest.Get("informationProtection/bitlocker/recoveryKeys",
                [("$filter", $"deviceId eq {ODataLiteral.String(id)}")], ClientHeaders);
            try
            {
                entries = await client.GetAllAsync<BitLockerRecoveryKeyDto>(list, maxPages: 1, cancellationToken).ConfigureAwait(false);
            }
            catch (GraphException error) when (IsAbsent(error))
            {
                entries = [];
            }
            if (entries.Count > 0) break;
        }

        var newest = entries
            .OrderByDescending(entry => entry.CreatedDateTime ?? DateTimeOffset.MinValue)
            .Take(MaxBitLockerKeys);

        var keys = new List<BitLockerRecoveryKey>();
        foreach (var entry in newest)
        {
            if (entry.Id is not { } entryId) continue;
            // Exactly as documented: the key is only returned for `$select=key`.
            var request = GraphRequest.Get($"informationProtection/bitlocker/recoveryKeys/{entryId}",
                [("$select", "key")], ClientHeaders);
            BitLockerRecoveryKeyDto detail;
            try
            {
                detail = await client.GetAsync<BitLockerRecoveryKeyDto>(request, cancellationToken).ConfigureAwait(false);
            }
            catch (GraphException error) when (IsAbsent(error))
            {
                continue;
            }
            if (string.IsNullOrEmpty(detail.Key)) continue;
            keys.Add(new BitLockerRecoveryKey(
                entryId,
                detail.Key,
                GraphJson.Meaningful(entry.CreatedDateTime ?? detail.CreatedDateTime),
                BitLockerRecoveryKey.VolumeName(entry.VolumeType ?? detail.VolumeType)));
        }
        return new BitLockerKeyLookup(keys, entries.Count, idsTried);
    }
}
