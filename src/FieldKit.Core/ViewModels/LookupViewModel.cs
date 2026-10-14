using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// The Devices page's header (ports <c>HomeView</c>): demo and storage notices, the walk in
/// progress, This PC and recent lookups. The box and list are <see cref="DeviceListViewModel"/>.
/// </summary>
public sealed partial class LookupViewModel : ObservableObject
{
    /// <summary>The demo serials worth trying, with what each shows.</summary>
    public static IReadOnlyList<(string Serial, string Shows)> DemoSerials { get; } =
    [
        ("5CG1234XYZ", "healthy"),
        ("PF2ABCDE", "problems"),
        ("PF9NEW01", "Autopilot only"),
    ];

    private readonly AppSession _session;
    private readonly ILocalStore _store;

    public LookupViewModel(AppSession session, ILocalStore store, ILocalDeviceInfo device)
    {
        _session = session;
        _store = store;
        ThisPcSerial = LocalDeviceSerial.Usable(device.SerialNumber);
        ThisPcName = device.MachineName;
        Recents = ReadRecents();
        // The tenant name arrives from Graph a moment after sign-in.
        session.PropertyChanged += SessionChanged;
    }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecents))]
    public partial IReadOnlyList<RecentLookupItem> Recents { get; private set; }

    public bool HasRecents => Recents.Count > 0;

    public bool HasThisPc => ThisPcSerial is not null;

    /// <summary>"DESKTOP-ABC · 5CG1234XYZ" under the This PC button.</summary>
    public string ThisPcDetail => $"{ThisPcName} · {ThisPcSerial}";

    public string ThisPcAutomationName => $"Look up this PC, {ThisPcName}, serial {ThisPcSerial}";

    /// <summary>This PC's serial, or null when the firmware has none worth looking up (the button is hidden).</summary>
    public string? ThisPcSerial { get; }

    public string ThisPcName { get; }

    public bool IsDemo => _session.IsDemo;

    public bool StorageIsTemporary => _store.IsTemporary;

    /// <summary>The newest site walk still in progress, offered for resuming (SPEC-windows §5).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveWalk), nameof(ActiveWalkText))]
    public partial Persistence.SiteWalk? ActiveWalk { get; private set; }

    public bool HasActiveWalk => ActiveWalk is not null;

    public string ActiveWalkText => ActiveWalk is { } walk ? $"Site walk in progress: {DeviceText.Join(" · ", walk.Site, walk.Area)}" : "";

    /// <summary>"Alex Doe · Contoso" for the page header.</summary>
    public string UserLine =>
        _session.User is { } user ? $"{user.DisplayName} · {user.TenantName ?? user.TenantId}" : "";


    /// <summary>Re-reads recent lookups, the walk in progress and the signed-in user, when the page is shown again.</summary>
    public void Refresh()
    {
        Recents = ReadRecents();
        ActiveWalk = _store.SiteWalks().FirstOrDefault(walk => walk.IsActive);
        OnPropertyChanged(nameof(UserLine));
        OnPropertyChanged(nameof(IsDemo));
    }

    /// <summary>Stops following the session once the page is gone.</summary>
    public void Detach() => _session.PropertyChanged -= SessionChanged;


    private IReadOnlyList<RecentLookupItem> ReadRecents() => _store.RecentLookups().Select(recent => RecentLookupItem.From(recent)).ToList();

    private void SessionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSession.User)) OnPropertyChanged(nameof(UserLine));
    }
}
