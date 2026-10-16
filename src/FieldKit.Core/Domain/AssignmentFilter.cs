namespace FieldKit.Core.Domain;

/// <summary>An Intune assignment filter: a rule on device properties that narrows an assignment.</summary>
/// <param name="Rule">The filter's rule as Intune shows it, e.g. <c>(device.model -startsWith "ThinkCentre")</c>.</param>
public sealed record AssignmentFilter(string Id, string Name, string Rule, string Platform);
