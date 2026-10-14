namespace FieldKit.Core.Domain;

/// <summary>
/// What to tell a tech when Entra has no LAPS password or BitLocker key for a device (Windows
/// only, 2026-10-14). The likely cause depends on how the device is joined: a hybrid joined
/// device often backs up to on-premises Active Directory instead.
/// </summary>
public static class MissingSecretText
{
    public static string Laps(string deviceName, EntraTrustType? trust) =>
        trust?.Kind switch
        {
            EntraTrustTypeKind.HybridJoined =>
                $"No LAPS password for {deviceName} is backed up to Microsoft Entra. It's hybrid joined, so Windows LAPS is probably backing up to on-premises Active Directory instead: look at the computer's LAPS tab in Active Directory Users and Computers, or run Get-LapsADPassword -Identity {deviceName} -AsPlainText. To back it up to Entra, set the LAPS policy's backup directory to Microsoft Entra ID.",
            EntraTrustTypeKind.Registered =>
                $"No LAPS password for {deviceName} is backed up to Microsoft Entra. It's only registered in Entra, not joined, and Windows LAPS backs up to Entra only from joined or hybrid joined devices.",
            _ =>
                $"No LAPS password for {deviceName} is backed up to Microsoft Entra. Check that Microsoft Entra LAPS is turned on for the tenant (Entra admin center → Devices → Device settings), that a Windows LAPS policy backing up to Microsoft Entra ID targets this device (Intune → Endpoint security → Account protection), and that the device has checked in since (Sync, then try again in a few minutes).",
        };

    /// <param name="idsTried">The Entra IDs FieldKit looked under, to compare with the portal.</param>
    public static string BitLocker(string deviceName, EntraTrustType? trust, bool? isEncrypted, IReadOnlyList<string> idsTried)
    {
        var looked = idsTried.Count == 0 ? "" : $" FieldKit looked for device ID {string.Join(" or ", idsTried)}.";
        if (isEncrypted == false)
        {
            return $"Intune reports {deviceName} isn't encrypted, so there's no BitLocker recovery key to show. Check the disk encryption policy assigned to it (Intune → Endpoint security → Disk encryption).{looked}";
        }
        return trust?.Kind == EntraTrustTypeKind.HybridJoined
            ? $"No BitLocker recovery key for {deviceName} is stored in Microsoft Entra. It's hybrid joined, so the key may be escrowed only to on-premises Active Directory (the computer's BitLocker Recovery tab in Active Directory Users and Computers) or to MBAM or Configuration Manager. To add it to Entra, have the disk encryption policy save recovery information to Microsoft Entra ID, or run BackupToAAD-BitLockerKeyProtector on the device.{looked}"
            : $"No BitLocker recovery key for {deviceName} is stored in Microsoft Entra. Check that BitLocker is on and that the disk encryption policy saves recovery information to Microsoft Entra ID. On the device, BackupToAAD-BitLockerKeyProtector backs up the current key straight away.{looked}";
    }
}
