using System.Diagnostics;
using FieldKit.App.Controls;
using FieldKit.Core.Persistence;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace FieldKit.App.Views;

/// <summary>
/// A site walk's summary, and saving its CSV where the user picks (FileSavePicker in place of
/// iOS's ShareLink). The file is UTF-8 with a BOM and CRLF line endings, so Excel opens it cleanly.
/// </summary>
public sealed partial class SiteWalkSummaryPage : Page
{
    private StorageFile? _saved;

    public SiteWalkSummaryPage()
    {
        InitializeComponent();
    }

    public SiteWalkSummaryViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = new SiteWalkSummaryViewModel((SiteWalk)e.Parameter, AppServices.Current.Store);
        Bindings.Update();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(ViewModel.FileName),
        };
        picker.FileTypeChoices.Add("CSV (comma separated)", [".csv"]);
        // Unpackaged desktop apps tell the picker which window owns it.
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.Window.Handle);
        try
        {
            if (await picker.PickSaveFileAsync() is not { } file) return;
            await File.WriteAllTextAsync(file.Path, ViewModel.Csv(), SiteWalkExporter.FileEncoding);
            _saved = file;
            ShowStatus($"Saved {file.Name}.", Tone.Good);
            OpenButton.Visibility = Visibility.Visible;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"CSV export failed: {error.GetType().Name}");
            ShowStatus("Couldn't save the CSV file. Check the folder isn't read-only and the file isn't open in Excel, then try again.", Tone.Caution);
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_saved is { } file) await Windows.System.Launcher.LaunchFileAsync(file);
    }

    private void ShowStatus(string text, Tone tone)
    {
        ExportStatus.Text = text;
        ExportStatus.Tone = tone;
        ExportStatus.Visibility = Visibility.Visible;
    }
}
