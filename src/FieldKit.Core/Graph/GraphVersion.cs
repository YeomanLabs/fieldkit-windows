namespace FieldKit.Core.Graph;

/// <summary>
/// v1.0 everywhere, except features Microsoft only exposes in beta (Settings Catalog,
/// scripts, remediations, hardware information, detected apps). Those load independently so a
/// beta change can't break the rest.
/// </summary>
public enum GraphVersion
{
    V1,
    Beta,
}
