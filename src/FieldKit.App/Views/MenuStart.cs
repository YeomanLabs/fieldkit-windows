using Microsoft.UI.Xaml.Navigation;

namespace FieldKit.App.Views;

/// <summary>
/// The navigation parameter for a section opened from its menu item. The Devices, Users and Groups
/// tabs start over when they get it (<see cref="IMenuTab"/>); Back and links leave a tab as it is.
/// </summary>
internal sealed class MenuStart
{
    public static readonly MenuStart Instance = new();

    private MenuStart()
    {
    }

    /// <summary>Whether a page was reached by choosing its menu item, not by Back or a link.</summary>
    public static bool Opened(NavigationEventArgs e) => e.NavigationMode == NavigationMode.New && e.Parameter is MenuStart;
}
