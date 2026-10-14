namespace FieldKit.Core.Domain;

public static class FlagSeverityExtensions
{
    public static string Title(this FlagSeverity severity) =>
        severity switch
        {
            FlagSeverity.Info => "Info",
            FlagSeverity.Warning => "Warning",
            FlagSeverity.Critical => "Critical",
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };

    /// <summary>The name iOS stores in scan records ("info", "warning", "critical").</summary>
    public static string RawValue(this FlagSeverity severity) => severity.Title().ToLowerInvariant();

    public static FlagSeverity? FromRawValue(string rawValue) =>
        Enum.GetValues<FlagSeverity>().Cast<FlagSeverity?>().FirstOrDefault(severity => severity!.Value.RawValue() == rawValue);
}
