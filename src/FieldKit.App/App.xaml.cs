using FieldKit.App.Auth;
using FieldKit.App.Platform;
using FieldKit.Core.Persistence;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;

namespace FieldKit.App;

public partial class App : Application
{
    private MainWindow? _window;

    /// <summary>The one window, for dialogs that act on it (the secrets dialog's capture exclusion).</summary>
    internal static MainWindow Window => ((App)Current)._window!;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        var demo = arguments.Any(argument => string.Equals(argument, "--demo", StringComparison.OrdinalIgnoreCase));

        var store = SqliteLocalStore.Open(DatabasePath());
        MainWindow? window = null;
        var session = new AppSession(store, config => new MsalAuthProvider(config, () => window?.Handle ?? IntPtr.Zero, store), demo: demo);
        AppServices.Current = new AppServices
        {
            Store = store,
            Settings = new SettingsStore(store),
            Session = session,
            DeviceInfo = new WindowsDeviceInfo(),
            DataFolder = Path.GetDirectoryName(DatabasePath())!,
            Version = AppChannel.Version,
            OwnerVerifier = new WindowsHelloVerifier(() => window?.Handle ?? IntPtr.Zero),
            Clipboard = new SecureClipboard(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()),
        };

        _window = window = new MainWindow();
        // --theme dark|light overrides the system theme (screenshots, and checking both without changing Windows).
        var theme = Array.IndexOf(arguments, "--theme") is var index and >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        if (_window.Content is FrameworkElement root && theme is not null)
        {
            if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)) root.RequestedTheme = ElementTheme.Dark;
            else if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase)) root.RequestedTheme = ElementTheme.Light;
        }
        _window.Activate();
        await session.RestoreAsync();
    }

    /// <summary>
    /// Per user, in %LOCALAPPDATA%\FieldKit (a test build: %LOCALAPPDATA%\FieldKit Test, so it never
    /// touches the public app's setup, recents or site walks). Uninstalling leaves it; "Clear all
    /// local data" empties it.
    /// </summary>
    private static string DatabasePath()
    {
#if DEBUG
        // Debug builds can use another folder (Start-FieldKit.ps1 -DataFolder), so trying things
        // in demo mode leaves the installed app's recents and site walks alone.
        if (Environment.GetEnvironmentVariable("FIELDKIT_DATA_FOLDER") is { Length: > 0 } folder) return Path.Combine(folder, "fieldkit.db");
#endif
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppChannel.Name, "fieldkit.db");
    }
}
