using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>One of the 15 boxes in the extension attributes dialog.</summary>
public sealed partial class ExtensionAttributeField : ObservableObject
{
    public ExtensionAttributeField(int number, string original)
    {
        Number = number;
        Original = original;
        Text = original;
    }

    public int Number { get; }

    /// <summary>The value when the dialog opened; empty when not set.</summary>
    public string Original { get; }

    /// <summary><c>extensionAttribute3</c>, as dynamic group rules spell it.</summary>
    public string Name => DeviceExtensionAttributes.Name(Number);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged), nameof(IsTooLong), nameof(StateText))]
    public partial string Text { get; set; }

    /// <summary>What to send: the trimmed text, or null to clear the attribute.</summary>
    public string? NewValue => Text.Trim() is { Length: > 0 } value ? value : null;

    public bool IsChanged => Text.Trim() != Original;

    public bool IsTooLong => Text.Trim().Length > DeviceExtensionAttributes.MaxLength;

    /// <summary>"New", "Changed" or "Cleared" beside a box that differs from Entra.</summary>
    public string StateText => !IsChanged ? "" : NewValue is null ? "Cleared" : Original.Length == 0 ? "New" : "Changed";
}
