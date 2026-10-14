using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// <c>GET /directory/deviceLocalCredentials/{deviceId}?$select=credentials</c> (Windows LAPS).
/// Everything is optional: only a credential with a readable password matters.
/// </summary>
internal sealed class DeviceLocalCredentialInfoDto
{
    internal sealed class CredentialDto
    {
        public string? AccountName { get; init; }

        public DateTimeOffset? BackupDateTime { get; init; }

        public string? PasswordBase64 { get; init; }
    }

    public string? Id { get; init; }

    public string? DeviceName { get; init; }

    public DateTimeOffset? LastBackupDateTime { get; init; }

    public DateTimeOffset? RefreshDateTime { get; init; }

    public IReadOnlyList<CredentialDto>? Credentials { get; init; }

    /// <summary>The most recently backed-up credential, or null if there's none with a readable password.</summary>
    public LocalAdminCredential? Newest()
    {
        var sorted = (Credentials ?? []).OrderByDescending(credential => credential.BackupDateTime ?? DateTimeOffset.MinValue);
        foreach (var credential in sorted)
        {
            if (credential.PasswordBase64 is not { } encoded
                || LocalAdminCredential.DecodePassword(encoded) is not { } password)
            {
                continue;
            }
            return new LocalAdminCredential(
                DeviceName ?? "",
                credential.AccountName ?? "Administrator",
                password,
                GraphJson.Meaningful(credential.BackupDateTime ?? LastBackupDateTime),
                GraphJson.Meaningful(RefreshDateTime));
        }
        return null;
    }
}
