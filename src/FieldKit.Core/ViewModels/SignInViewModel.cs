using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FieldKit.Core.ViewModels;

/// <summary>The sign-in page (ports <c>SignInView</c>; Windows has no paywall gate, as the app is free).</summary>
public sealed partial class SignInViewModel : ObservableObject
{
    private readonly AppSession _session;

    public SignInViewModel(AppSession session)
    {
        _session = session;
        session.PropertyChanged += SessionChanged;
    }

    public string ButtonText => _session.IsDemo ? "Enter demo" : "Sign in with Microsoft";

    public string SetupLinkText =>
        _session.IsDemo ? "Leave demo and set up a tenant"
        : _session.Config is { } config ? $"Tenant: {config.TenantId}"
        : "Tenant setup";

    public bool IsSigningIn => _session.IsSigningIn;

    public string? Message => _session.SignInMessage;

    public bool HasMessage => Message is not null;

    public bool CanSignIn => _session.Auth.IsConfigured && !_session.IsSigningIn;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private Task SignInAsync() => _session.SignInAsync();

    [RelayCommand]
    private Task ChangeSetupAsync() => _session.BeginSetupAsync();

    public void Detach() => _session.PropertyChanged -= SessionChanged;

    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSession.IsSigningIn):
                OnPropertyChanged(nameof(IsSigningIn));
                OnPropertyChanged(nameof(CanSignIn));
                SignInCommand.NotifyCanExecuteChanged();
                break;
            case nameof(AppSession.SignInMessage):
                OnPropertyChanged(nameof(Message));
                OnPropertyChanged(nameof(HasMessage));
                break;
            case nameof(AppSession.IsDemo) or nameof(AppSession.Config):
                OnPropertyChanged(nameof(ButtonText));
                OnPropertyChanged(nameof(SetupLinkText));
                break;
        }
    }
}
