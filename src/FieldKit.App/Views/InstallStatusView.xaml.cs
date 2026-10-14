using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>The device page's install status tab: each app's install result and each configuration policy's.</summary>
public sealed partial class InstallStatusView : UserControl
{
    public InstallStatusView()
    {
        InitializeComponent();
    }

    public InstallStatusViewModel? ViewModel { get; private set; }

    public void Show(InstallStatusViewModel model)
    {
        ViewModel = model;
        Bindings.Update();
    }
}
