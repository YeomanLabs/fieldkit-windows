using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>What to do with an Autopilot record's assigned user (ports <c>AutopilotEditModel.UserChange</c>).</summary>
public abstract record AutopilotUserChange
{
    private AutopilotUserChange()
    {
    }

    public static AutopilotUserChange Keep { get; } = new KeepUser();

    public static AutopilotUserChange Remove { get; } = new RemoveUser();

    public sealed record KeepUser : AutopilotUserChange;

    public sealed record Assign(DirectoryUser User) : AutopilotUserChange;

    public sealed record RemoveUser : AutopilotUserChange;
}
