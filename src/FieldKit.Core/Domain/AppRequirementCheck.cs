namespace FieldKit.Core.Domain;

/// <summary>One of a Win32 app's requirements against the device.</summary>
/// <param name="Needs">"Windows 10 1607 or later".</param>
/// <param name="DeviceHas">"build 26100", or "not reported" when Intune doesn't say.</param>
/// <param name="Met">Null when FieldKit can't tell.</param>
public sealed record AppRequirementCheck(string Label, string Needs, string DeviceHas, bool? Met);
