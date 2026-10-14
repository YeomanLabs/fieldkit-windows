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

    public async Task LoadAsync(bool forceRefresh = false)
    {
        State = PermissionsState.Loading;
        try
        {
            var scopes = await _auth.GrantedScopesAsync(forceRefresh);
            if (GraphPermission.Granted(scopes) is { } granted)
            {
                Rows = GraphPermission.All.Select(permission => new PermissionRow(permission, granted.Contains(permission.Name))).ToList();
                State = PermissionsState.Loaded;
            }
            else
            {
                Rows = [];
                State = PermissionsState.Unknown;
            }
            if (forceRefresh) RefreshedAt = _time.GetLocalNow();
        }
        catch (AuthException error)
        {
            Message = error.UserMessage;
            State = PermissionsState.Failed;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Message = new AuthException(AuthErrorKind.Failed, error.Message).UserMessage;
            State = PermissionsState.Failed;
        }
    }

    private bool CanRefresh => State != PermissionsState.Loading;

    /// <summary>Gets a new token, picking up permissions granted since sign-in.</summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => LoadAsync(forceRefresh: true);
}
