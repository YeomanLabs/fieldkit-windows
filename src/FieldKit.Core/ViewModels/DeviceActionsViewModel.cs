using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Phase 3: remote actions and recovery secrets for one device (ports <c>DeviceActionsModel</c>).
/// Windows Hello gates everything except sync. Secrets live only in <see cref="Revealed"/> and
/// are dropped when the secrets dialog closes.
/// </summary>
public sealed partial class DeviceActionsViewModel : ObservableObject
{
    private readonly DeviceActions _actions;
    private readonly RecoverySecrets _secrets;
    private readonly IOwnerVerifier _verifier;
    private readonly Action _onUnauthorized;

    public DeviceActionsViewModel(DeviceActions actions, RecoverySecrets secrets, IOwnerVerifier verifier, Action onUnauthorized)
    {
        _actions = actions;
        _secrets = secrets;
        _verifier = verifier;
        _onUnauthorized = onUnauthorized;
        Status = ActionStatus.Idle;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(HasStatus), nameof(StatusText), nameof(StatusIsFailure), nameof(StatusIsDone))]
    public partial ActionStatus Status { get; private set; }

    public bool IsRunning => Status.IsRunning;

    public bool HasStatus => Status is not ActionStatus.IdleStatus;

    public bool StatusIsFailure => Status is ActionStatus.Failed;

    public bool StatusIsDone => Status is ActionStatus.Done;

    public string StatusText => Status switch
    {
        ActionStatus.Running running => running.Text,
        ActionStatus.Done done => done.Text,
        ActionStatus.Failed failed => failed.Text,
        _ => "",
    };

    /// <summary>Set after Hello and a successful fetch; the page opens the secrets dialog for it.</summary>
    [ObservableProperty]
    public partial RevealedSecrets? Revealed { get; private set; }

    /// <summary>Retire is irreversible, so the device name has to be typed back first (ports <c>RetireConfirmSheet</c>).</summary>
    public static bool RetireNameMatches(string typed, string deviceName) =>
        deviceName.Length > 0 && string.Equals(typed.Trim(), deviceName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs a remote action after any required Windows Hello. Returns true when Graph accepted it.</summary>
    public async Task<bool> RunAsync(DeviceAction action, IntuneDevice device)
    {
        if (IsRunning) return false;
        if (action.RequiresOwnerVerification() && await _verifier.VerifyAsync($"{action.Title()} {device.DeviceName}") is { } failure)
        {
            Status = new ActionStatus.Failed(failure);
            return false;
        }
        Status = new ActionStatus.Running($"Sending {action.Title().ToLowerInvariant()}…");
        try
        {
            await _actions.PerformAsync(action, device.Id);
            Debug.WriteLine($"Device action sent: {action}");
            Status = new ActionStatus.Done(action.SuccessMessage());
            return true;
        }
        catch (Exception error)
        {
            Fail(error, GraphFeature.DeviceActions);
            return false;
        }
    }

    /// <summary>Runs a remediation on the device now, after Windows Hello. Returns true when Graph accepted it.</summary>
    public async Task<bool> RunRemediationAsync(AssignedItem script, IntuneDevice device)
    {
        if (!await UnlockAsync($"Run {script.Name} on {device.DeviceName}")) return false;
        Status = new ActionStatus.Running("Sending remediation…");
        try
        {
            await _actions.RunRemediationAsync(script.Id, device.Id);
            Debug.WriteLine("Remediation run sent");
            Status = new ActionStatus.Done($"{script.Name} sent. The device runs it when it next checks in; results appear in Intune under Remediations → Device status.");
            return true;
        }
        catch (Exception error)
        {
            Fail(error, GraphFeature.RunRemediation);
            return false;
        }
    }

    /// <param name="trust">How the device is joined, to say where its password probably is when Entra has none.</param>
    public async Task ShowLocalAdminPasswordAsync(string entraDeviceId, string deviceName, EntraTrustType? trust = null)
    {
        if (!await UnlockAsync($"Show the LAPS password for {deviceName}")) return;
        Status = new ActionStatus.Running("Fetching LAPS password…");
        try
        {
            if (await _secrets.LocalAdminCredentialAsync(entraDeviceId) is { } credential)
            {
                Status = ActionStatus.Idle;
                Revealed = new RevealedSecrets.LocalAdmin(credential);
            }
            else
            {
                Status = new ActionStatus.Failed(MissingSecretText.Laps(deviceName, trust));
            }
        }
        catch (Exception error)
        {
            Fail(error, GraphFeature.LocalAdminPassword);
        }
    }

    /// <param name="isEncrypted">Intune's view, so an unencrypted device says so instead of "no key".</param>
    public async Task ShowBitLockerKeysAsync(string entraDeviceId, string? entraObjectId, string deviceName, EntraTrustType? trust = null, bool? isEncrypted = null)
    {
        if (!await UnlockAsync($"Show BitLocker recovery keys for {deviceName}")) return;
        Status = new ActionStatus.Running("Fetching BitLocker keys…");
        try
        {
            var lookup = await _secrets.BitLockerKeysAsync(entraDeviceId, entraObjectId);
            if (lookup.Keys.Count > 0)
            {
                Status = ActionStatus.Idle;
                Revealed = new RevealedSecrets.BitLocker(deviceName, lookup.Keys);
            }
            else if (lookup.ListedCount > 0)
            {
                Status = new ActionStatus.Failed($"Entra listed {lookup.ListedCount} BitLocker key(s) for this device but didn't return the key values. Check that your Entra role can read BitLocker keys (and is active, if you use PIM).");
            }
            else
            {
                Status = new ActionStatus.Failed(MissingSecretText.BitLocker(deviceName, trust, isEncrypted, lookup.IdsTried));
            }
        }
        catch (Exception error)
        {
            Fail(error, GraphFeature.BitLockerKeys);
        }
    }

    /// <summary>The secrets dialog closed: drop the values.</summary>
    public void ForgetRevealed() => Revealed = null;

    public void ClearStatus()
    {
        if (!IsRunning) Status = ActionStatus.Idle;
    }

    private async Task<bool> UnlockAsync(string reason)
    {
        if (IsRunning) return false;
        if (await _verifier.VerifyAsync(reason) is not { } failure) return true;
        Status = new ActionStatus.Failed(failure);
        return false;
    }

    private void Fail(Exception error, GraphFeature feature)
    {
        switch (error)
        {
            case OperationCanceledException:
                Status = ActionStatus.Idle;
                break;
            case GraphException { Kind: GraphErrorKind.Unauthorized } unauthorized:
                _onUnauthorized();
                Status = new ActionStatus.Failed(unauthorized.UserMessage);
                break;
            case GraphException graph:
                Status = new ActionStatus.Failed(feature.Message(graph));
                break;
            default:
                Status = new ActionStatus.Failed(GraphException.Network().UserMessage);
                break;
        }
    }
}
