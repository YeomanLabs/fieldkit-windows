namespace FieldKit.Core.Domain;

public static class ConditionalAccessStatuses
{
    public static ConditionalAccessStatus FromRaw(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "success" => ConditionalAccessStatus.Success,
            "failure" => ConditionalAccessStatus.Failure,
            "notapplied" => ConditionalAccessStatus.NotApplied,
            _ => ConditionalAccessStatus.Unknown,
        };
}
