namespace FieldKit.Core.Domain;

/// <summary>The cases of <see cref="Lookup{T}"/>.</summary>
public enum LookupState
{
    /// <summary>Still loading or failed; flags that depend on it are not evaluated. The default.</summary>
    Unavailable,
    Found,
    NotFound,
    /// <summary>Deliberately not run, e.g. no Entra device ID to look up.</summary>
    Skipped,
}
