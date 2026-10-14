namespace FieldKit.Core.Graph;

/// <summary>
/// The only HTTP methods FieldKit sends. POST, PATCH and DELETE are used only by approved write
/// features (Autopilot updates, device actions, group membership, device extension attributes),
/// plus POST to Intune's report actions (<c>deviceManagement/reports/*</c>), which are reads.
/// </summary>
public enum GraphMethod
{
    Get,
    Post,
    Patch,
    Delete,
}
