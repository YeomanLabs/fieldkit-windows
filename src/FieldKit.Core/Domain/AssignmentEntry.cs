namespace FieldKit.Core.Domain;

/// <summary>One assignment on an Intune object: a target and, for apps, an intent.</summary>
/// <param name="Intent">"required", "available", "uninstall", … for apps; null for policies.</param>
public sealed record AssignmentEntry(AssignmentTarget Target, string? Intent = null);
