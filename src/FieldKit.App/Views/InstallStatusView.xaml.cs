using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>The device page's install status tab: each app's install result and each configuration policy's. Problem apps link to "Why didn't it install?".</summary>
public sealed partial class InstallStatusView : UserControl
{
    public InstallStatusView()
    {
        InitializeComponent();
    }

    public InstallStatusViewModel? ViewModel { get; private set; }

    /// <summary>Raised when an app's "Why …?" link is clicked; the hosting page navigates.</summary>
    public event EventHandler<AppStatusItem>? AppInvoked;

    public void Show(InstallStatusViewModel model)
    {
        ViewModel = model;
        Bindings.Update();
    }

    private void Why_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement { DataContext: AppStatusItem item }) AppInvoked?.Invoke(this, item);
    }
}
