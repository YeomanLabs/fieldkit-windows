using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Edit an Entra device's extensionAttribute1–15 (Windows only; approved 2026-10-13). Save sends
/// only the attributes that changed, in one PATCH; an emptied box clears its attribute.
/// </summary>
public sealed partial class ExtensionAttributesEditViewModel : ObservableObject
{
    private readonly EntraDevice _device;
    private readonly EntraDeviceUpdates _updates;
    private readonly Action _onUnauthorized;

    public ExtensionAttributesEditViewModel(EntraDevice device, EntraDeviceUpdates updates, Action onUnauthorized)
    {
        _device = device;
        _updates = updates;
        _onUnauthorized = onUnauthorized;
        Fields = Enumerable.Range(1, DeviceExtensionAttributes.Count)
            .Select(number => new ExtensionAttributeField(number, device.ExtensionAttributes[number]))
            .ToList();
        foreach (var box in Fields) box.PropertyChanged += FieldChanged;
    }

    public IReadOnlyList<ExtensionAttributeField> Fields { get; }

    public string DeviceName => _device.DisplayName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    public bool HasChanges => Fields.Any(box => box.IsChanged);

    public bool CanSave => HasChanges && !IsSaving;

    /// <summary>"1 change" or "3 changes", beside Save.</summary>
    public string ChangeSummary => Fields.Count(box => box.IsChanged) switch
    {
        0 => "No changes",
        1 => "1 change",
        var count => $"{count} changes",
    };

    /// <summary>The attributes as Graph now has them, once a save succeeded.</summary>
    public DeviceExtensionAttributes? Saved { get; private set; }

    /// <summary>Sends the changes. Returns true when Graph accepted them.</summary>
    public async Task<bool> SaveAsync()
    {
        if (Fields.FirstOrDefault(box => box.IsTooLong) is { } tooLong)
        {
            ErrorMessage = $"{tooLong.Name} is longer than {DeviceExtensionAttributes.MaxLength} characters.";
            return false;
        }
        var changes = Fields.Where(box => box.IsChanged).ToDictionary(box => box.Number, box => box.NewValue);
        if (changes.Count == 0) return false;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await _updates.SetExtensionAttributesAsync(_device.Id, changes);
            Saved = _device.ExtensionAttributes.With(changes);
            return true;
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.ExtensionAttributes) is { } message) ErrorMessage = message;
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void FieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExtensionAttributeField.Text)) return;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(ChangeSummary));
    }
}
