using System.Diagnostics;
using FieldKit.Core.Services;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;

namespace FieldKit.App.Platform;

/// <summary>
/// Copies a LAPS password or BitLocker key (ports <c>SecureClipboard</c>; SPEC-windows §8): kept
/// out of clipboard history and cloud clipboard, and cleared after 60 seconds if the clipboard
/// still holds it. If clearing fails, it's retried when FieldKit is next activated. The value
/// isn't flushed, so it also goes when FieldKit closes.
/// </summary>
internal sealed class SecureClipboard : ISecureClipboard
{
    private readonly DispatcherQueueTimer _timer;
    private uint? _ourSequence;
    private bool _clearPending;

    public SecureClipboard(DispatcherQueue dispatcher)
    {
        _timer = dispatcher.CreateTimer();
        _timer.Interval = ISecureClipboard.Lifetime;
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => ClearIfStillOurs();
    }

    public void Copy(string secret)
    {
        var package = new DataPackage();
        package.SetText(secret);
        Clipboard.SetContentWithOptions(package, new ClipboardContentOptions { IsAllowedInHistory = false, IsRoamable = false });
        _ourSequence = NativeMethods.GetClipboardSequenceNumber();
        _clearPending = false;
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Called when the window is activated: finishes a clear that failed earlier.</summary>
    public void OnActivated()
    {
        if (_clearPending) ClearIfStillOurs();
    }

    private void ClearIfStillOurs()
    {
        // Anything copied since (by the user or another app) is left alone.
        if (_ourSequence is not { } ours || NativeMethods.GetClipboardSequenceNumber() != ours)
        {
            _ourSequence = null;
            _clearPending = false;
            return;
        }
        try
        {
            Clipboard.Clear();
            _ourSequence = null;
            _clearPending = false;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Debug.WriteLine($"Clearing the clipboard failed ({error.GetType().Name}); retrying on activation.");
            _clearPending = true;
        }
    }
}
