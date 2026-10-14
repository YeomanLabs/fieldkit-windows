namespace FieldKit.Core.Domain;

/// <summary>A configuration policy's result on a device, worst first.</summary>
public enum PolicyDeploymentState
{
    Error,
    Conflict,
    NotReported,
    Unknown,
    Succeeded,
    NotApplicable,
    NotAssigned,
}
