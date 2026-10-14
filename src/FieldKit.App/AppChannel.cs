using System.Reflection;

namespace FieldKit.App;

/// <summary>
/// Which build this is (the user's request on 2026-10-14). A public release is "FieldKit". A test
/// build (<c>Build-Release.ps1 -Test</c>) is "FieldKit Test": the same code, installed beside the
/// public app with its own folder, Start menu entry and local data, and labelled in the title bar,
/// so a version can be tried on a real tenant before it's released.
/// </summary>
internal static class AppChannel
{
#if FIELDKIT_TEST
    public static bool IsTest { get; } = true;
#else
    public static bool IsTest { get; } = false;
#endif

    /// <summary>"FieldKit" or "FieldKit Test": the window, the local data folder and the sign-in prompt.</summary>
    public static string Name => IsTest ? "FieldKit Test" : "FieldKit";

    /// <summary>"1.4.2", or a test build's "1.5.0-test.1".</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(AppChannel).Assembly;
        // The build appends "+<commit>"; people only need the version.
        if (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is { Length: > 0 } informational)
        {
            return informational.Split('+')[0];
        }
        return assembly.GetName().Version?.ToString(3) ?? "?";
    }
}
