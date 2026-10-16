namespace FieldKit.Core.Domain;

/// <summary>One entry in an Intune Management Extension log (CMTrace format).</summary>
/// <param name="At">The device's local time; logs don't say which time zone.</param>
/// <param name="Severity">1 information, 2 warning, 3 error.</param>
public sealed record ImeLogLine(string Message, DateTime? At, string Component, int Severity, int Thread)
{
    public bool IsProblem => Severity >= 2;
}
