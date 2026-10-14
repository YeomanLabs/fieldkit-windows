using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>"Sign in with Microsoft" (or "Enter demo"), and the way back to tenant setup.</summary>
public sealed partial class SignInPage : Page
{
    public SignInPage()
    {
        ViewModel = new SignInViewModel(AppServices.Current.Session);
        InitializeComponent();
        Loaded += (_, _) => SignInButton.Focus(FocusState.Programmatic);
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public SignInViewModel ViewModel { get; }
}
