using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary><c>GET /users</c> item.</summary>
internal sealed class UserDto
{
    public const string SelectFields = "id,displayName,userPrincipalName,mail,jobTitle,department";

    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? UserPrincipalName { get; init; }

    public string? Mail { get; init; }

    public string? JobTitle { get; init; }

    public string? Department { get; init; }

    public DirectoryUser ToDomain() =>
        new()
        {
            Id = Id,
            DisplayName = DisplayName ?? UserPrincipalName ?? "",
            UserPrincipalName = UserPrincipalName ?? "",
            Mail = Mail ?? "",
            JobTitle = JobTitle ?? "",
            Department = Department ?? "",
        };
}
