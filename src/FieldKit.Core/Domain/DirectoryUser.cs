namespace FieldKit.Core.Domain;

public sealed record DirectoryUser
{
    /// <summary>Always set by Graph; defaults only so XAML can create the type.</summary>
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string UserPrincipalName { get; init; } = "";

    public string Mail { get; init; } = "";

    public string JobTitle { get; init; } = "";

    public string Department { get; init; } = "";

    /// <summary>Job title and department, for a second line under the name.</summary>
    public string Subtitle => string.Join(" · ", new[] { JobTitle, Department }.Where(part => part.Length > 0));
}
