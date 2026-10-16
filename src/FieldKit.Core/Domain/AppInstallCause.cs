namespace FieldKit.Core.Domain;

/// <summary>One possible reason an app isn't where it should be.</summary>
/// <param name="Likely">The evidence points straight at it; otherwise it's worth ruling out.</param>
public sealed record AppInstallCause(string Title, string Detail, bool Likely);
