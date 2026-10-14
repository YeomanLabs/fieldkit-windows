using FieldKit.App.Platform;
using FieldKit.Core.Services;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Views;

/// <summary>
/// A LAPS password or BitLocker keys (ports <c>SecretsSheet</c>; SPEC-windows §8). While it's
/// open the window is left out of screenshots, recordings and screen sharing; values are masked
/// until shown and masked again when FieldKit loses focus; it closes after two minutes.
/// </summary>
public sealed partial class SecretsDialog : ContentDialog
{
    private readonly MainWindow _window;
    private readonly DispatcherQueueTimer _lifetime;
    private readonly List<DispatcherQueueTimer> _copyTimers = [];

    public SecretsDialog(RevealedSecrets secrets, MainWindow window)
    {
        ViewModel = new SecretsViewModel(secrets);
        _window = window;
        InitializeComponent();
        _lifetime = DispatcherQueue.CreateTimer();
        _lifetime.Interval = SecretsViewModel.Lifetime;
        _lifetime.IsRepeating = false;
        _lifetime.Tick += (_, _) => Hide();
        Opened += Dialog_Opened;
        Closed += Dialog_Closed;
    }

    public SecretsViewModel ViewModel { get; }

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        NativeMethods.SetWindowDisplayAffinity(_window.Handle, NativeMethods.WdaExcludeFromCapture);
        _window.Activated += Window_Activated;
        _lifetime.Start();
    }

    private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        _lifetime.Stop();
        foreach (var timer in _copyTimers) timer.Stop();
        _window.Activated -= Window_Activated;
        ViewModel.MaskAll();
        NativeMethods.SetWindowDisplayAffinity(_window.Handle, NativeMethods.WdaNone);
    }

    /// <summary>Switching away (Alt+Tab, another window) hides every value.</summary>
    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated) ViewModel.MaskAll();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SecretValue secret }) secret.ToggleVisibility();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SecretValue secret }) return;
        secret.CopyTo(AppServices.Current.Clipboard);
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = ISecureClipboard.Lifetime;
        timer.IsRepeating = false;
        timer.Tick += (_, _) => secret.CopyExpired();
        timer.Start();
        _copyTimers.Add(timer);
    }
}
