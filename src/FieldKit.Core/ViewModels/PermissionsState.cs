namespace FieldKit.Core.ViewModels;

/// <summary>The cases of <see cref="PermissionsViewModel.State"/>.</summary>
public enum PermissionsState
{
    Loading,
    Loaded,
    /// <summary>The token didn't say which permissions it carries.</summary>
    Unknown,
    Failed,
}
