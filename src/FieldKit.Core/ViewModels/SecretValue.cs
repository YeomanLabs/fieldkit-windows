using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// One secret in the secrets dialog (ports <c>SecretValueRow</c>): masked until shown, masked
/// again when the window loses focus, copied only through <see cref="ISecureClipboard"/>.
/// </summary>
public sealed partial class SecretValue : ObservableObject
{
    private readonly string _secret;

    public SecretValue(string label, string secret)
    {
        Label = label;
        _secret = secret;
    }

    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText), nameof(ToggleText), nameof(AutomationText))]
    public partial bool IsVisible { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyText))]
    public partial bool IsCopied { get; private set; }

    /// <summary>The value when shown; otherwise bullets (at most 16, so the length isn't given away).</summary>
    public string DisplayText => IsVisible ? _secret : new string('•', Math.Min(_secret.Length, 16));

    /// <summary>What Narrator reads: "Hidden" rather than a row of bullets.</summary>
    public string AutomationText => IsVisible ? $"{Label}: {_secret}" : $"{Label}: hidden";

    public string ToggleText => IsVisible ? "Hide" : "Show";

    public string CopyText => IsCopied ? "Copied: clears in 60 seconds" : "Copy";

    public void ToggleVisibility() => IsVisible = !IsVisible;

    public void Mask() => IsVisible = false;

    public void CopyTo(ISecureClipboard clipboard)
    {
        clipboard.Copy(_secret);
        IsCopied = true;
    }

    /// <summary>After the clipboard's lifetime, the button says "Copy" again.</summary>
    public void CopyExpired() => IsCopied = false;

    /// <summary>Leaves the value out, so logging can't leak it.</summary>
    public override string ToString() => $"SecretValue {{ Label = {Label} }}";
}
