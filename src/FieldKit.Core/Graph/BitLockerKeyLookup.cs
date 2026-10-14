using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>The result of looking for a device's BitLocker keys, with enough detail to explain an empty result.</summary>
/// <param name="ListedCount">Keys Entra listed for the device, including any whose value it didn't return.</param>
/// <param name="IdsTried">The IDs searched, in order (Entra device ID, then object ID).</param>
public sealed record BitLockerKeyLookup(IReadOnlyList<BitLockerRecoveryKey> Keys, int ListedCount, IReadOnlyList<string> IdsTried);
