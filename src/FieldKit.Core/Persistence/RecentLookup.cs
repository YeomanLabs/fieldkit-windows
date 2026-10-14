namespace FieldKit.Core.Persistence;

/// <summary>A serial looked up recently, for the Lookup page.</summary>
public sealed record RecentLookup(string Serial, string? DeviceName, DateTimeOffset LookedUpAt);
