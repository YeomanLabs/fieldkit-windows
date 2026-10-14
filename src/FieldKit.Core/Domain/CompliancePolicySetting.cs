namespace FieldKit.Core.Domain;

public sealed record CompliancePolicySetting(
    string Name,
    PolicyComplianceState State,
    string? CurrentValue = null,
    string? ErrorDescription = null)
{
    /// <summary>
    /// Graph often leaves <c>settingName</c> empty and sends an ID like
    /// "Windows10CompliancePolicy.bitLockerEnabled"; fall back to its last part.
    /// </summary>
    public static string DisplayName(string? settingName, string? setting)
    {
        if (!string.IsNullOrEmpty(settingName)) return settingName;
        if (string.IsNullOrEmpty(setting)) return "Unnamed setting";
        return setting.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? setting;
    }
}
