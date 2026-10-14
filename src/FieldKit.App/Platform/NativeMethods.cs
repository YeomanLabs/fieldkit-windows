using System.Runtime.InteropServices;

namespace FieldKit.App.Platform;

/// <summary>The few Win32 calls WinUI doesn't cover.</summary>
internal static class NativeMethods
{
    /// <summary>The window shows on the monitor but not in screenshots, recordings or screen sharing (Windows 10 2004+).</summary>
    public const uint WdaExcludeFromCapture = 0x11;

    public const uint WdaNone = 0;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    /// <summary>Changes whenever anything writes to the clipboard, so FieldKit can tell its secret is still there without reading it.</summary>
    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();
}
