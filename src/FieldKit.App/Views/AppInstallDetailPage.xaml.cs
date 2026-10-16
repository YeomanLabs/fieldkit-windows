using FieldKit.Core.Demo;
using FieldKit.Core.Services;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;

namespace FieldKit.App.Views;

/// <summary>"Why didn't it install?": one app on one device explained (opened from the install status tab).</summary>
public sealed partial class AppInstallDetailPage : Page
{
    public AppInstallDetailPage()
    {
        InitializeComponent();
    }

    public AppInstallDetailViewModel ViewModel { get; private set; } = null!;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var services = AppServices.Current;
        var session = services.Session;
        // Demo mode reads no files: its logs are canned too.
        IDeviceLogReader logs = session.IsDemo ? new DemoDeviceLogReader() : services.DeviceLogs;
        ViewModel = new AppInstallDetailViewModel((AppInstallSubject)e.Parameter, session.AppInstall, session.SessionExpired, logs: logs);
        Bindings.Update();
        await ViewModel.LoadAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.Cancel();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    /// <summary>
    /// A zip from the device's Collect diagnostics in Intune, or log files copied from the device
    /// (AppWorkload.log and the one before it). Only the Intune Management Extension's logs are read.
    /// </summary>
    private async void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add(".zip");
        picker.FileTypeFilter.Add(".log");
        // Unpackaged desktop apps tell the picker which window owns it.
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.Window.Handle);
        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0) await ViewModel.ReadLogFilesAsync(files.Select(file => file.Path).ToList());
    }
}
