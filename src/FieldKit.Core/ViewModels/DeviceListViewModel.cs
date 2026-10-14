using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// The Devices tab's list: every Intune device A–Z, or devices whose name starts with what's
/// typed. A typed or scanned serial is also looked up exactly, so it shows in the list, and
/// Enter opens it (see <see cref="Submit"/>).
/// </summary>
public sealed partial class DeviceListViewModel(DeviceLookup lookup, Action onUnauthorized, Func<TimeSpan, CancellationToken, Task>? delay = null)
    : BrowseViewModel<IntuneDevice>(onUnauthorized, delay)
{
    public override string EmptyText => "No devices are enrolled in Intune.";

    public override string NoMatchText => "No device names start with that, and no device has that serial. Press Enter to look the serial up anyway.";

    protected override string AllHeading => "All devices";

    protected override GraphFeature? Feature => null;

    /// <summary>Why Enter didn't open anything.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEnterError))]
    public partial string? EnterError { get; private set; }

    public bool HasEnterError => EnterError is not null;

    /// <summary>
    /// What Enter opens: the listed device named exactly as typed, otherwise the text as a serial
    /// (what a USB scanner sends, often before the search has run). Null when it's neither.
    /// </summary>
    public DeviceTarget? EnterTarget()
    {
        var text = Query.Trim();
        if (Items.FirstOrDefault(device => string.Equals(device.DeviceName, text, StringComparison.OrdinalIgnoreCase)) is { } named
            && DeviceText.LookupSerial(named) is { } namedSerial)
        {
            return new DeviceTarget(namedSerial, named.Id);
        }
        return SerialNormalizer.Normalize(text) is { } serial ? new DeviceTarget(serial, null) : null;
    }

    /// <summary>
    /// Enter in the box (ports Lookup's submit): the device to open, with the box cleared for the
    /// next scan, or null with <see cref="EnterError"/> saying why. An empty box does nothing.
    /// </summary>
    public DeviceTarget? Submit()
    {
        if (EnterTarget() is not { } target)
        {
            EnterError = Query.Trim().Length == 0 ? null : "That isn't a listed device name or a serial (5–30 letters, numbers or dashes).";
            return null;
        }
        Query = "";
        return target;
    }

    protected override void OnQueryEdited() => EnterError = null;

    protected override Task<GraphPage<IntuneDevice>> FirstPageAsync(CancellationToken cancellationToken) =>
        lookup.DevicesPageAsync(cancellationToken: cancellationToken);

    protected override Task<GraphPage<IntuneDevice>> NextPageAsync(Uri next, CancellationToken cancellationToken) =>
        lookup.DevicesPageAsync(next, cancellationToken);

    /// <summary>An exact serial match when the text reads as a serial, then name-prefix matches A–Z.</summary>
    protected override async Task<IReadOnlyList<IntuneDevice>> SearchAsync(string term, CancellationToken cancellationToken)
    {
        var byName = lookup.SearchDevicesAsync(term, SearchLimit, cancellationToken);
        var bySerial = SerialNormalizer.Normalize(term) is { } serial
            ? lookup.IntuneDevicesAsync(serial, cancellationToken)
            : Task.FromResult<IReadOnlyList<IntuneDevice>>([]);
        await Task.WhenAll(byName, bySerial);
        var byNameSorted = (await byName).OrderBy(device => device.DeviceName, StringComparer.CurrentCultureIgnoreCase);
        return (await bySerial).Concat(byNameSorted).DistinctBy(device => device.Id).ToList();
    }

    protected override string SearchHeading(string term) => $"Devices matching “{term}”";
}
