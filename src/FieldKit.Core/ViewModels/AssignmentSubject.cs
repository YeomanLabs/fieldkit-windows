using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>Whose groups and assignments to show (ports <c>AssignmentSubject</c>).</summary>
public abstract record AssignmentSubject
{
    private AssignmentSubject()
    {
    }

    public abstract string Title { get; }

    /// <param name="EntraObjectId">Null when the device has no Entra object.</param>
    /// <param name="PrimaryUserUpn">Adds user-targeted assignments.</param>
    public sealed record Device(string Name, string? EntraObjectId, string? PrimaryUserUpn) : AssignmentSubject
    {
        public override string Title => Name;
    }

    public sealed record Person(DirectoryUser User) : AssignmentSubject
    {
        public override string Title => User.DisplayName;
    }
}
