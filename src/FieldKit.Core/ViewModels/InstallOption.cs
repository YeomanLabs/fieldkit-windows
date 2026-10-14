using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>A group that would install an app if the device or user joined it (ports <c>InstallOption</c>).</summary>
/// <param name="AlsoTargets">Other apps, profiles and policies this group also brings in.</param>
public sealed record InstallOption(DirectoryGroup Group, IReadOnlyList<string> AlsoTargets)
{
    public string Id => Group.Id;
}
