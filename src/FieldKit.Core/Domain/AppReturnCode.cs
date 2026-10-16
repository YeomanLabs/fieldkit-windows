namespace FieldKit.Core.Domain;

/// <summary>An installer exit code a Win32 app maps to a result: "success", "softReboot", "hardReboot", "retry" or "failed".</summary>
public sealed record AppReturnCode(int Code, string Type)
{
    /// <summary>"3010 (soft restart)".</summary>
    public string Text => Type.ToLowerInvariant() switch
    {
        "success" => $"{Code} (success)",
        "softreboot" => $"{Code} (soft restart)",
        "hardreboot" => $"{Code} (hard restart)",
        "retry" => $"{Code} (retry)",
        "failed" => $"{Code} (failed)",
        _ => $"{Code} ({Type})",
    };
}
