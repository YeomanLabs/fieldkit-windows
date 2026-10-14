namespace FieldKit.Core.Services;

/// <summary>
/// Copies a secret so it stays out of clipboard history and cloud sync, and clears itself
/// (ports <c>SecureClipboard</c>; SPEC-windows §8).
/// </summary>
public interface ISecureClipboard
{
    /// <summary>How long a copied secret stays on the clipboard.</summary>
    static TimeSpan Lifetime => TimeSpan.FromSeconds(60);

    void Copy(string secret);
}
