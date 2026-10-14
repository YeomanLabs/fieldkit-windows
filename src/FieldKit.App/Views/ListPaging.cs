using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FieldKit.App.Views;

/// <summary>Fetches a tab's next page when its list is scrolled close to the end.</summary>
internal static class ListPaging
{
    /// <summary>How near the end, in screens, the next page starts loading.</summary>
    private const double Screens = 1.5;

    public static void LoadMoreNearEnd(ListView list, Func<bool> canLoad, Func<Task> load)
    {
        ScrollViewer? attached = null;
        list.Loaded += (_, _) =>
        {
            // Loaded runs on every visit to a cached page; hook the scroller once.
            if (attached is not null || Find<ScrollViewer>(list) is not { } scroller) return;
            attached = scroller;
            scroller.ViewChanged += async (_, _) =>
            {
                if (!canLoad() || scroller.VerticalOffset < scroller.ScrollableHeight - scroller.ViewportHeight * Screens) return;
                await load();
            };
        };
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if ((child as T ?? Find<T>(child)) is { } found) return found;
        }
        return null;
    }
}
