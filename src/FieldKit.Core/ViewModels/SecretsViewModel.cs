namespace FieldKit.Core.ViewModels;

/// <summary>
/// The secrets dialog's content (ports <c>SecretsSheet</c>): a LAPS password or BitLocker keys.
/// The dialog masks every value when FieldKit loses focus and closes after <see cref="Lifetime"/>.
/// </summary>
public sealed class SecretsViewModel
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(120);

    public SecretsViewModel(RevealedSecrets secrets)
    {
        switch (secrets)
        {
            case RevealedSecrets.LocalAdmin local:
                var credential = local.Credential;
                Title = "LAPS password";
                var dates = new List<SecretDetail> { new("Backed up", DateText.RelativeAndAbsolute(credential.BackedUpAt)) };
                if (credential.RotatesAt is { } rotatesAt) dates.Add(new SecretDetail("Next rotation", DateText.Absolute(rotatesAt)));
                Sections =
                [
                    new SecretSection("",
                        [new("Device", credential.DeviceName), new("Account", credential.AccountName)],
                        [new SecretValue("Password", credential.Password)], ""),
                    new SecretSection("", dates, [], ""),
                ];
                break;
            case RevealedSecrets.BitLocker bitLocker:
                Title = "BitLocker keys";
                Sections =
                [
                    new SecretSection("", [new("Device", bitLocker.DeviceName)], [], ""),
                    .. bitLocker.Keys.Select(key => new SecretSection(
                        key.Volume,
                        [new("Key ID", KeyIdPrefix(key.Id)), new("Created", DateText.Absolute(key.CreatedAt))],
                        [new SecretValue("Recovery key", key.Key)],
                        "Match the key ID to the first 8 characters shown on the BitLocker recovery screen.")),
                ];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(secrets));
        }
    }

    public string Title { get; }

    public IReadOnlyList<SecretSection> Sections { get; }

    public IEnumerable<SecretValue> AllSecrets => Sections.SelectMany(section => section.Secrets);

    public string Note =>
        "Values hide when FieldKit loses focus, and this closes after two minutes. Copied values stay out of clipboard history, don't sync to your other devices, and clear after 60 seconds.";

    /// <summary>Hides every value (the window lost focus).</summary>
    public void MaskAll()
    {
        foreach (var secret in AllSecrets) secret.Mask();
    }

    /// <summary>The recovery screen shows the key ID's first 8 characters, upper case.</summary>
    public static string KeyIdPrefix(string id) => (id.Length > 8 ? id[..8] : id).ToUpperInvariant();
}
