using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// A person and the Intune devices they're the primary user of (ports <c>PersonModel</c>), plus
/// their recent sign-ins for "why was I blocked?" (Windows only). Each section loads and fails on its own.
/// </summary>
public sealed partial class PersonViewModel : ObservableObject
{
    private readonly DirectoryLookup _directory;
    private readonly SignInLookup? _signIns;
    private readonly Action _onUnauthorized;
    private readonly TimeProvider _time;

    public PersonViewModel(DirectoryUser person, DirectoryLookup directory, Action onUnauthorized, SignInLookup? signIns = null, TimeProvider? time = null)
    {
        Person = person;
        _directory = directory;
        _signIns = signIns;
        _onUnauthorized = onUnauthorized;
        _time = time ?? TimeProvider.System;
        Devices = SectionState<IReadOnlyList<IntuneDevice>>.Loading;
        SignIns = signIns is null ? SectionState<IReadOnlyList<SignInItem>>.Skipped("Sign-ins aren't available here.") : SectionState<IReadOnlyList<SignInItem>>.Loading;
        OnlyProblems = true;
    }

    public DirectoryUser Person { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceList))]
    public partial SectionState<IReadOnlyList<IntuneDevice>> Devices { get; private set; }

    public IReadOnlyList<IntuneDevice> DeviceList => Devices.Kind == SectionKind.Loaded ? Devices.Value! : [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignInList), nameof(ShowsNoProblems), nameof(SignInSummary))]
    public partial SectionState<IReadOnlyList<SignInItem>> SignIns { get; private set; }

    /// <summary>Show only failed, blocked and interrupted sign-ins (the default: that's why you're looking).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignInList), nameof(ShowsNoProblems))]
    public partial bool OnlyProblems { get; set; }

    private IReadOnlyList<SignInItem> AllSignIns => SignIns.Kind == SectionKind.Loaded ? SignIns.Value! : [];

    public IReadOnlyList<SignInItem> SignInList => OnlyProblems ? AllSignIns.Where(item => item.Diagnosis.IsProblem).ToList() : AllSignIns;

    /// <summary>Loaded, filtered to problems, and there are none.</summary>
    public bool ShowsNoProblems => SignIns.Kind == SectionKind.Loaded && OnlyProblems && !AllSignIns.Any(item => item.Diagnosis.IsProblem);

    /// <summary>"2 problems in 14 sign-ins over the last 7 days."</summary>
    public string SignInSummary
    {
        get
        {
            if (SignIns.Kind != SectionKind.Loaded) return "";
            var problems = AllSignIns.Count(item => item.Diagnosis.IsProblem);
            var signIns = AllSignIns.Count == 1 ? "1 sign-in" : $"{AllSignIns.Count} sign-ins";
            var limit = AllSignIns.Count >= SignInLookup.Limit ? $" (the latest {SignInLookup.Limit})" : "";
            return problems switch
            {
                0 => $"No problems in {signIns}{limit} over the last {SignInLookup.Days} days.",
                1 => $"1 problem in {signIns}{limit} over the last {SignInLookup.Days} days.",
                _ => $"{problems} problems in {signIns}{limit} over the last {SignInLookup.Days} days.",
            };
        }
    }

    /// <summary>Mail is shown only when it differs from the UPN.</summary>
    public bool ShowsEmail =>
        Person.Mail.Length > 0 && !string.Equals(Person.Mail, Person.UserPrincipalName, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    public Task LoadAsync() => Task.WhenAll(LoadDevicesAsync(), LoadSignInsAsync());

    [RelayCommand]
    public async Task LoadDevicesAsync()
    {
        Devices = SectionState<IReadOnlyList<IntuneDevice>>.Loading;
        try
        {
            var found = await _directory.ManagedDevicesAsync(Person.Id);
            Devices = found.Count == 0
                ? SectionState<IReadOnlyList<IntuneDevice>>.NotFound
                : SectionState<IReadOnlyList<IntuneDevice>>.Loaded(
                    found.OrderByDescending(device => device.LastSyncDateTime ?? DateTimeOffset.MinValue).ToList());
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized) is not { } message) return;
            Devices = SectionState<IReadOnlyList<IntuneDevice>>.Failed(message);
        }
    }

    [RelayCommand]
    public async Task LoadSignInsAsync()
    {
        if (_signIns is null) return;
        SignIns = SectionState<IReadOnlyList<SignInItem>>.Loading;
        try
        {
            var found = await _signIns.RecentAsync(Person.Id);
            var now = _time.GetUtcNow();
            SignIns = found.Count == 0
                ? SectionState<IReadOnlyList<SignInItem>>.NotFound
                : SectionState<IReadOnlyList<SignInItem>>.Loaded(found.Select(signIn => SignInItem.From(signIn, now)).ToList());
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.SignIns) is not { } message) return;
            SignIns = SectionState<IReadOnlyList<SignInItem>>.Failed(message);
        }
    }
}
