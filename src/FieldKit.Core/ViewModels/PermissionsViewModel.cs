using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldKit.Core.Auth;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>
/// Which Graph permissions the current sign-in actually has, for troubleshooting "Access
/// denied" after an admin changes the app registration (ports <c>PermissionsModel</c>).
/// </summary>
public sealed partial class PermissionsViewModel : ObservableObject
{
    private readonly IAuthProvider _auth;
    private readonly TimeProvider _time;

    public PermissionsViewModel(IAuthProvider auth, TimeProvider? time = null)
    {
        _auth = auth;
        _time = time ?? TimeProvider.System;
        Rows = [];
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    public partial PermissionsState State { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<PermissionRow> Rows { get; private set; }

    /// <summary>Why the check failed, for <see cref="PermissionsState.Failed"/>.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset? RefreshedAt { get; private set; }

    public Task LoadAsync(bool forceRefresh = false) => ShowAsync(() => _auth.GrantedScopesAsync(forceRefresh), forceRefresh);

    private bool CanRefresh => State != PermissionsState.Loading;

    /// <summary>
    /// Gets a new token, picking up permissions granted since sign-in. When Windows can't do that
    /// silently (a permission newly added to the app registration, MFA, Conditional Access), it
    /// shows Microsoft's sign-in prompt instead of stopping at "Sign in to continue".
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        await LoadAsync(forceRefresh: true);
        if (State == PermissionsState.Failed && _needsSignIn) await ShowAsync(() => _auth.GrantedScopesInteractiveAsync(), refreshed: true);
    }

    /// <summary>The last failure was Windows wanting the user, not an error.</summary>
    private bool _needsSignIn;

    private async Task ShowAsync(Func<Task<IReadOnlyList<string>>> scopes, bool refreshed)
    {
        State = PermissionsState.Loading;
        _needsSignIn = false;
        try
        {
            if (GraphPermission.Granted(await scopes()) is { } granted)
            {
                Rows = GraphPermission.All.Select(permission => new PermissionRow(permission, granted.Contains(permission.Name))).ToList();
                State = PermissionsState.Loaded;
            }
            else
            {
                Rows = [];
                State = PermissionsState.Unknown;
            }
            if (refreshed) RefreshedAt = _time.GetLocalNow();
        }
        catch (AuthException error)
        {
            _needsSignIn = error.Kind is AuthErrorKind.InteractionRequired or AuthErrorKind.NoAccount;
            Message = error.UserMessage;
            State = PermissionsState.Failed;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Message = new AuthException(AuthErrorKind.Failed, error.Message).UserMessage;
            State = PermissionsState.Failed;
        }
    }
}
