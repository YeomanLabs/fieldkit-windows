using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Auth;
using FieldKit.Core.Setup;

namespace FieldKit.Core.ViewModels;

/// <summary>First-launch tenant setup: where to enter the IDs, and how to get them (ports <c>SetupView</c>).</summary>
public sealed partial class SetupViewModel : ObservableObject
{
    private readonly AppSession _session;

    public SetupViewModel(AppSession session)
    {
        _session = session;
        TenantId = session.Config?.TenantId ?? "";
        ClientId = session.Config?.ClientId ?? "";
        session.PropertyChanged += SessionChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string TenantId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(Guide))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string ClientId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Message), nameof(HasMessage))]
    public partial string? Problem { get; private set; }

    /// <summary>A problem with what was typed, or why sign-in couldn't be set up with it.</summary>
    public string? Message => Problem ?? _session.SetupMessage;

    public bool HasMessage => Message is not null;

    /// <summary>Follows the client ID box, so the redirect URI fills in once a valid ID is typed.</summary>
    public SetupGuide Guide => new(ClientId);

    public string ExistingRegistrationNote => SetupGuide.ExistingRegistrationNote;

    public string NoSecretNote => SetupGuide.NoSecretNote;

    public string ConditionalAccessNote => SetupGuide.ConditionalAccessNote;

    public Uri AdminCenterUrl => SetupGuide.AdminCenterUrl;

    public bool CanSave => TenantId.Trim().Length > 0 && ClientId.Trim().Length > 0;

    /// <summary>Setup can be left without changes once a tenant (or demo) is already set up.</summary>
    public bool CanCancel => _session.Auth.IsConfigured;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var (config, problem) = AuthConfig.Validated(ClientId, TenantId);
        if (problem is { } found)
        {
            Problem = found.Message();
            return;
        }
        _session.ApplySetup(config!);
    }

    [RelayCommand]
    private Task StartDemoAsync() => _session.StartDemoAsync();

    [RelayCommand]
    private void Cancel() => _session.CancelSetup();

    /// <summary>Stops following the session once the page is gone.</summary>
    public void Detach() => _session.PropertyChanged -= SessionChanged;

    partial void OnTenantIdChanged(string value) => Problem = null;

    partial void OnClientIdChanged(string value) => Problem = null;

    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppSession.SetupMessage)) return;
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(HasMessage));
    }
}
