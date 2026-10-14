using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>Secrets fetched after Windows Hello, held only while the secrets dialog is open (ports <c>RevealedSecrets</c>).</summary>
public abstract record RevealedSecrets
{
    private RevealedSecrets()
    {
    }

    public sealed record LocalAdmin(LocalAdminCredential Credential) : RevealedSecrets;

    public sealed record BitLocker(string DeviceName, IReadOnlyList<BitLockerRecoveryKey> Keys) : RevealedSecrets;

    /// <summary>Names the kind only, so logging it can't leak a value.</summary>
    public override string ToString() => GetType().Name;
}
