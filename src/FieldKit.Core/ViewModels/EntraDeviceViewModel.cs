using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Opens a device reached through a group: finds its Intune record by Entra device ID, so the
/// normal device page can show it (ports <c>EntraDeviceView</c>).
/// </summary>
public sealed partial class EntraDeviceViewModel : ObservableObject
{
    private readonly DeviceLookup _lookup;
    private readonly Action _onUnauthorized;

    public EntraDeviceViewModel(string deviceId, string name, DeviceLookup lookup, Action onUnauthorized)
    {
        DeviceId = deviceId;
        Name = name;
        _lookup = lookup;
        _onUnauthorized = onUnauthorized;
        State = SectionState<IntuneDevice>.Loading;
    }

    public string DeviceId { get; }

    public string Name { get; }

    /// <summary>Loaded with the record and its normalised serial; NotFound when Intune has no record.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Message), nameof(IsLoading))]
    public partial SectionState<IntuneDevice> State { get; private set; }

    public bool IsLoading => State.IsLoading;

    public string Title => State.Kind switch
    {
        SectionKind.NotFound or SectionKind.Skipped => "Not in Intune",
        SectionKind.Failed => $"Couldn't open {Name}",
        _ => Name,
    };

    public string Message => State.Kind switch
    {
        SectionKind.NotFound or SectionKind.Skipped => $"{Name} is in Entra but has no Intune record.",
        SectionKind.Failed => State.Message ?? "",
        _ => "",
    };

    public async Task ResolveAsync()
    {
        State = SectionState<IntuneDevice>.Loading;
        try
        {
            var records = await _lookup.IntuneDevicesForEntraDeviceAsync(DeviceId);
            State = records.FirstOrDefault() is { } device && SerialNormalizer.Normalize(device.SerialNumber) is { } serial
                ? SectionState<IntuneDevice>.Loaded(device with { SerialNumber = serial })
                : SectionState<IntuneDevice>.NotFound;
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized) is not { } message) return;
            State = SectionState<IntuneDevice>.Failed(message);
        }
    }
}
