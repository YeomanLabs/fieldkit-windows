using System.ComponentModel;
using System.Runtime.InteropServices;
using FieldKit.App.Views;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace FieldKit.App;

/// <summary>The one window: a custom title bar over setup, sign-in or the signed-in shell.</summary>
public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 820;
    private const int MinimumWidth = 900;
    private const int MinimumHeight = 600;

    private readonly AppSession _session = AppServices.Current.Session;
    private ShellPage? _shell;

    public MainWindow()
    {
        InitializeComponent();
        // Taskbar and Alt+Tab say which one this is when a test build runs beside the public app.
        Title = AppChannel.Name;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        // Taskbar and Alt+Tab; the exe carries the same icon for the Start menu and Installed apps.
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        SizeAndCenter();
        _session.PropertyChanged += SessionChanged;
        Closed += (_, _) => _session.PropertyChanged -= SessionChanged;
        Activated += (_, args) =>
        {
            // A secret's clipboard clear that failed while FieldKit was in the background.
            if (args.WindowActivationState != WindowActivationState.Deactivated) AppServices.Current.Clipboard.OnActivated();
        };
        ShowState();
    }

    /// <summary>The window handle, for MSAL's broker prompt.</summary>
    public IntPtr Handle => WindowNative.GetWindowHandle(this);

    /// <summary>Called by the shell as its page history changes.</summary>
    internal void SetBackButton(bool visible) => AppTitleBar.IsBackButtonVisible = visible;

    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSession.State) or nameof(AppSession.IsDemo)) ShowState();
    }

    private void ShowState()
    {
        // "Test build 1.5.0-test.1 · Demo data": a test build always says so, next to demo mode.
        AppTitleBar.Subtitle = string.Join(" · ", new[]
        {
            AppChannel.IsTest ? $"Test build {AppChannel.Version}" : "",
            _session.IsDemo ? "Demo data" : "",
        }.Where(part => part.Length > 0));
        if (_session.State == SessionState.SignedIn)
        {
            if (_shell is not null && ReferenceEquals(Root.Content, _shell)) return;
            _shell = new ShellPage(this);
            Root.Content = _shell;
            AppTitleBar.IsPaneToggleButtonVisible = true;
            return;
        }
        _shell = null;
        AppTitleBar.IsPaneToggleButtonVisible = false;
        AppTitleBar.IsBackButtonVisible = false;
        Root.Content = _session.State switch
        {
            SessionState.NeedsSetup => new SetupPage(),
            SessionState.SignedOut => new SignInPage(),
            _ => Restoring(),
        };
    }

    private static UIElement Restoring() =>
        new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12,
            Children =
            {
                new ProgressRing { IsActive = true },
                new TextBlock { Text = "Signing in…", Style = (Style)Application.Current.Resources["SecondaryTextStyle"] },
            },
        };

    private void TitleBar_BackRequested(TitleBar sender, object args) => _shell?.GoBack();

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args) => _shell?.TogglePane();

    /// <summary>1280 × 820 effective pixels, centred, never smaller than 900 × 600.</summary>
    private void SizeAndCenter()
    {
        var scale = GetDpiForWindow(Handle) / 96.0;
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var width = Math.Min((int)(DefaultWidth * scale), workArea.Width);
        var height = Math.Min((int)(DefaultHeight * scale), workArea.Height);
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(MinimumWidth * scale);
            presenter.PreferredMinimumHeight = (int)(MinimumHeight * scale);
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
