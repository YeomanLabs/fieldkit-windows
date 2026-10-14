using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// The device page (ports <c>DeviceDetailModel</c>). Intune and Autopilot load in parallel;
/// Entra and compliance follow the selected Intune record. Each section loads and fails on its own.
/// </summary>
public sealed partial class DeviceDetailViewModel : ObservableObject
{
    private readonly DeviceLookup _lookup;
    private readonly SettingsStore _settings;
    private readonly ILocalStore _store;
    private readonly Action _onUnauthorized;
    private readonly TimeProvider _time;
    private CancellationTokenSource _cancellation = new();

    public DeviceDetailViewModel(
        string serial,
        string? preferredIntuneId,
        DeviceLookup lookup,
        SettingsStore settings,
        ILocalStore store,
        Action onUnauthorized,
        TimeProvider? time = null)
    {
        Serial = serial;
        SelectedIntuneId = preferredIntuneId;
        _lookup = lookup;
        _settings = settings;
        _store = store;
        _onUnauthorized = onUnauthorized;
        _time = time ?? TimeProvider.System;
        Intune = SectionState<IReadOnlyList<IntuneDevice>>.Loading;
        Entra = SectionState<EntraDevice>.Loading;
        Autopilot = SectionState<IReadOnlyList<AutopilotDevice>>.Loading;
        Compliance = SectionState<IReadOnlyList<CompliancePolicyState>>.Loading;
        _settings.PropertyChanged += SettingsChanged;
    }

    public string Serial { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IntuneRecords), nameof(HasDuplicateRecords), nameof(SelectedIntune), nameof(HasStorage), nameof(Flags), nameof(HasFlags), nameof(IsLoading), nameof(HasFailedSection), nameof(Title), nameof(Subtitle), nameof(CanShowAssignments), nameof(HasIntuneRecord), nameof(IsWindows), nameof(HasEntraDeviceId))]
    public partial SectionState<IReadOnlyList<IntuneDevice>> Intune { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EntraDevice), nameof(ExtensionAttributeList), nameof(HasExtensionAttributes), nameof(CanEditExtensionAttributes), nameof(Flags), nameof(HasFlags), nameof(IsLoading), nameof(HasFailedSection), nameof(Title), nameof(CanShowAssignments))]
    public partial SectionState<EntraDevice> Entra { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutopilotRecords), nameof(Flags), nameof(HasFlags), nameof(IsLoading), nameof(HasFailedSection), nameof(Subtitle))]
    public partial SectionState<IReadOnlyList<AutopilotDevice>> Autopilot { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompliancePolicies), nameof(HasCompliancePolicies))]
    public partial SectionState<IReadOnlyList<CompliancePolicyState>> Compliance { get; private set; }

    /// <summary>Which Intune record is shown when the serial has duplicates.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedIntune), nameof(HasStorage), nameof(Flags), nameof(HasFlags), nameof(Title), nameof(Subtitle), nameof(CanShowAssignments), nameof(HasIntuneRecord), nameof(IsWindows), nameof(HasEntraDeviceId))]
    public partial string? SelectedIntuneId { get; private set; }

    /// <summary>Shown under the Autopilot card after an edit, since Autopilot is slow to reflect changes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutopilotNotice))]
    public partial string? AutopilotNotice { get; private set; }

    public bool HasAutopilotNotice => AutopilotNotice is not null;

    /// <summary>Shown under the Entra card after extension attributes are saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntraNotice))]
    public partial string? EntraNotice { get; private set; }

    public bool HasEntraNotice => EntraNotice is not null;

    /// <summary>The Entra device's extension attributes that have a value (Windows only).</summary>
    public IReadOnlyList<ExtensionAttribute> ExtensionAttributeList => EntraDevice?.ExtensionAttributes.Set ?? [];

    public bool HasExtensionAttributes => ExtensionAttributeList.Count > 0;

    /// <summary>Extension attributes are written to the Entra object, so it has to have loaded.</summary>
    public bool CanEditExtensionAttributes => EntraDevice is not null;

    public IReadOnlyList<IntuneDevice> IntuneRecords => Intune.Kind == SectionKind.Loaded ? Intune.Value! : [];

    public bool HasDuplicateRecords => IntuneRecords.Count > 1;

    public IntuneDevice? SelectedIntune =>
        IntuneRecords.FirstOrDefault(record => record.Id == SelectedIntuneId) ?? IntuneRecords.FirstOrDefault();

    public EntraDevice? EntraDevice => Entra.Kind == SectionKind.Loaded ? Entra.Value : null;

    public IReadOnlyList<AutopilotDevice> AutopilotRecords => Autopilot.Kind == SectionKind.Loaded ? Autopilot.Value! : [];

    public IReadOnlyList<CompliancePolicyState> CompliancePolicies => Compliance.Kind == SectionKind.Loaded ? Compliance.Value! : [];

    public IReadOnlyList<Flag> Flags =>
        FlagEvaluator.Evaluate(Intune.ToLookup(), Entra.ToLookup(), Autopilot.ToLookup(), _settings.AuditSettings, SelectedIntuneId, _time.GetLocalNow());

    public bool HasFlags => Flags.Count > 0;

    public bool HasCompliancePolicies => CompliancePolicies.Count > 0;

    public bool HasStorage => SelectedIntune?.Storage is not null;

    public bool IsLoading => Intune.IsLoading || Entra.IsLoading || Autopilot.IsLoading;

    /// <summary>
    /// A section flags depend on failed, so "no flags" doesn't mean "no problems". (iOS says
    /// "No problems found" here; Windows says the checks are incomplete.)
    /// </summary>
    public bool HasFailedSection =>
        Intune.Kind == SectionKind.Failed || Entra.Kind == SectionKind.Failed || Autopilot.Kind == SectionKind.Failed;

    /// <summary>The Entra object ID, when the Entra device loaded; a fallback for BitLocker lookups.</summary>
    public string? EntraObjectId => EntraDevice?.Id;

    /// <summary>The Entra device ID (<c>azureADDeviceId</c>) of the selected record, used for LAPS and BitLocker.</summary>
    public string? EntraDeviceId => SelectedIntune is { HasEntraDeviceId: true } device ? device.AzureADDeviceId : null;

    /// <summary>
    /// Groups and assignments need the Intune record, and wait for Entra, which supplies the
    /// device's object ID (iOS disables the link the same way).
    /// </summary>
    public bool CanShowAssignments => SelectedIntune is not null && !Entra.IsLoading;

    /// <summary>Whose groups the assignments tab shows: the selected record, its Entra object and primary user.</summary>
    public AssignmentSubject? AssignmentSubject =>
        SelectedIntune is { } device
            ? new AssignmentSubject.Device(device.DeviceName, EntraObjectId, device.UserPrincipalName.Length == 0 ? null : device.UserPrincipalName)
            : null;
    /// <summary>Remote actions need the Intune record.</summary>
    public bool HasIntuneRecord => SelectedIntune is not null;

    /// <summary>Remediations run on Windows devices only (iOS hides the action otherwise).</summary>
    public bool IsWindows => SelectedIntune is { } device && string.Equals(device.OperatingSystem, "windows", StringComparison.OrdinalIgnoreCase);

    /// <summary>LAPS and BitLocker are looked up by the Entra device ID.</summary>
    public bool HasEntraDeviceId => EntraDeviceId is not null;
    /// <summary>Best name for the header and recent lookups.</summary>
    public string? DisplayName =>
        SelectedIntune is { DeviceName.Length: > 0 } device ? device.DeviceName
        : EntraDevice is { DisplayName.Length: > 0 } entra ? entra.DisplayName
        : null;

    public string Title => DisplayName ?? Serial;

    /// <summary>"5CG1234XYZ · Windows 10.0.26100.4652", or the Autopilot model before enrolment.</summary>
    public string Subtitle =>
        DeviceText.Join(" · ", Serial,
            SelectedIntune is { } device ? DeviceText.OperatingSystem(device)
            : AutopilotRecords.FirstOrDefault() is { } record ? DeviceText.Join(" ", record.Manufacturer, record.Model)
            : "");

    /// <summary>Loads every section, then records the lookup for the Lookup page.</summary>
    public async Task LoadAsync()
    {
        var autopilot = LoadAutopilotAsync();
        await LoadIntuneAsync();
        await autopilot;
        if (!_cancellation.IsCancellationRequested)
        {
            _store.RecordLookup(Serial, DisplayName, _time.GetUtcNow());
        }
    }

    [RelayCommand]
    public async Task LoadIntuneAsync()
    {
        var cancellationToken = _cancellation.Token;
        Intune = SectionState<IReadOnlyList<IntuneDevice>>.Loading;
        Entra = SectionState<EntraDevice>.Loading;
        Compliance = SectionState<IReadOnlyList<CompliancePolicyState>>.Loading;
        try
        {
            var records = await _lookup.IntuneDevicesAsync(Serial, cancellationToken);
            if (records.Count == 0)
            {
                NotInIntune();
                return;
            }
            if (!records.Any(record => record.Id == SelectedIntuneId))
            {
                SelectedIntuneId = records[0].Id;
            }
            Intune = SectionState<IReadOnlyList<IntuneDevice>>.Loaded(records);
            await LoadRecordDetailsAsync();
        }
        catch (GraphException error) when (error.Kind == GraphErrorKind.NotFound)
        {
            NotInIntune();
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized) is not { } message) return;
            Intune = SectionState<IReadOnlyList<IntuneDevice>>.Failed(message);
            Entra = SectionState<EntraDevice>.Skipped("Needs the Intune record first.");
            Compliance = SectionState<IReadOnlyList<CompliancePolicyState>>.Skipped("Needs the Intune record first.");
        }
    }

    [RelayCommand]
    public async Task LoadComplianceAsync()
    {
        if (SelectedIntune is not { } device) return;
        Compliance = SectionState<IReadOnlyList<CompliancePolicyState>>.Loading;
        try
        {
            var policies = await _lookup.CompliancePoliciesAsync(device.Id, _cancellation.Token);
            Compliance = SectionState<IReadOnlyList<CompliancePolicyState>>.Loaded(CompliancePolicyState.SortedForDisplay(policies));
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.ComplianceDetails) is not { } message) return;
            Compliance = SectionState<IReadOnlyList<CompliancePolicyState>>.Failed(message);
        }
    }

    [RelayCommand]
    public async Task LoadEntraAsync()
    {
        if (SelectedIntune is not { } device) return;
        if (!device.HasEntraDeviceId || device.AzureADDeviceId is not { } deviceId)
        {
            Entra = SectionState<EntraDevice>.Skipped("Intune has no Entra device ID for this record.");
            return;
        }
        Entra = SectionState<EntraDevice>.Loading;
        EntraNotice = null;
        try
        {
            Entra = await _lookup.EntraDeviceAsync(deviceId, _cancellation.Token) is { } found
                ? SectionState<EntraDevice>.Loaded(found)
                : SectionState<EntraDevice>.NotFound;
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized) is not { } message) return;
            Entra = SectionState<EntraDevice>.Failed(message);
        }
    }

    [RelayCommand]
    public async Task LoadAutopilotAsync()
    {
        Autopilot = SectionState<IReadOnlyList<AutopilotDevice>>.Loading;
        try
        {
            var records = await _lookup.AutopilotDevicesAsync(Serial, _cancellation.Token);
            Autopilot = records.Count == 0
                ? SectionState<IReadOnlyList<AutopilotDevice>>.NotFound
                : SectionState<IReadOnlyList<AutopilotDevice>>.Loaded(records);
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized) is not { } message) return;
            Autopilot = SectionState<IReadOnlyList<AutopilotDevice>>.Failed(message);
        }
    }

    /// <summary>After the Autopilot edit dialog saved: say so, and reload the records.</summary>
    public Task AutopilotSavedAsync()
    {
        AutopilotNotice = "Saved. Autopilot can take a few minutes to show the change.";
        return LoadAutopilotAsync();
    }

    /// <summary>
    /// After the extension attributes dialog saved: show what Graph accepted. Not re-read, since
    /// a read straight after the write can still return the old values.
    /// </summary>
    public void ExtensionAttributesSaved(DeviceExtensionAttributes saved)
    {
        if (EntraDevice is not { } device) return;
        Entra = SectionState<EntraDevice>.Loaded(device with { ExtensionAttributes = saved });
        EntraNotice = "Saved. Dynamic groups and filters that use these can take a few minutes to catch up.";
    }

    public Task SelectIntuneRecordAsync(string id)
    {
        if (id == SelectedIntuneId) return Task.CompletedTask;
        SelectedIntuneId = id;
        return LoadRecordDetailsAsync();
    }

    /// <summary>Stops in-flight loads when the page goes away; a later load starts fresh.</summary>
    public void Cancel()
    {
        _cancellation.Cancel();
        _cancellation = new CancellationTokenSource();
    }

    public void Detach()
    {
        Cancel();
        _settings.PropertyChanged -= SettingsChanged;
    }

    /// <summary>Entra and compliance both hang off the selected Intune record.</summary>
    private async Task LoadRecordDetailsAsync()
    {
        var compliance = LoadComplianceAsync();
        await LoadEntraAsync();
        await compliance;
    }

    private void NotInIntune()
    {
        Intune = SectionState<IReadOnlyList<IntuneDevice>>.NotFound;
        Entra = SectionState<EntraDevice>.Skipped("No Intune record to link to Entra.");
        Compliance = SectionState<IReadOnlyList<CompliancePolicyState>>.Skipped("Not enrolled in Intune.");
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsStore.AuditSettings)) return;
        OnPropertyChanged(nameof(Flags));
        OnPropertyChanged(nameof(HasFlags));
    }
}
