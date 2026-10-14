using FieldKit.Core.Auth;

namespace FieldKit.Core.ViewModels;

/// <summary>One permission FieldKit needs, and whether the current sign-in has it.</summary>
public sealed record PermissionRow(GraphPermission Permission, bool IsGranted)
{
    public string Status => IsGranted ? "Granted" : Permission.IsOptional ? "Not granted (optional)" : "Missing";

    /// <summary>Missing and needed: shown as a problem. An optional one that isn't granted isn't.</summary>
    public bool IsMissing => !IsGranted && !Permission.IsOptional;
}