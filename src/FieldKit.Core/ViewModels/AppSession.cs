using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Auth;
using FieldKit.Core.Demo;
using FieldKit.Core.Graph;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;

namespace FieldKit.Core.ViewModels;

/// <summary>Sign-in state and the shared services features are built from.</summary>
public sealed partial class AppSession : ObservableObject
{
    private readonly ILocalStore _store;
    private readonly Func<AuthConfig?, IAuthProvider> _makeAuthProvider;
    private readonly Func<IAuthProvider, IGraphClient> _makeGraphClient;
    private IGraphClient _graph;

    /// <param name="makeAuthProvider">Builds sign-in for a tenant (MSAL in the app). Null config means "not set up".</param>
    /// <param name="makeGraphClient">Builds the Graph client for a sign-in; tests replace it.</param>
    /// <param name="demo">Start in demo mode (<c>--demo</c>).</param>
    public AppSession(
        ILocalStore store,
        Func<AuthConfig?, IAuthProvider> makeAuthProvider,
        Func<IAuthProvider, IGraphClient>? makeGraphClient = null,
        bool demo = false)
    {
        _store = store;
        _makeAuthProvider = makeAuthProvider;
        _makeGraphClient = makeGraphClient ?? (auth => new GraphClient(auth));
        if (demo)
        {
            Auth = new DemoAuthProvider();
            _graph = new DemoGraphClient();
            IsDemo = true;
        }
        else
        {
            Config = AuthConfigStore.Saved(store);
            Auth = makeAuthProvider(Config);
            _graph = _makeGraphClient(Auth);
        }
        AssignmentCatalog = new AssignmentCatalog(_graph);
        State = SessionState.Restoring;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    public partial SessionState State { get; private set; }

    /// <summary>Set while <see cref="State"/> is <see cref="SessionState.SignedIn"/>.</summary>
    [ObservableProperty]
    public partial SignedInUser? User { get; private set; }

    [ObservableProperty]
    public partial bool IsSigningIn { get; private set; }

    /// <summary>The tenant setup sign-in uses; null before setup (or in demo mode).</summary>
    [ObservableProperty]
    public partial AuthConfig? Config { get; private set; }

    /// <summary>Sample data instead of a tenant; shown with a banner so it's never mistaken for real.</summary>
    [ObservableProperty]
    public partial bool IsDemo { get; private set; }

    [ObservableProperty]
    public partial string? SignInMessage { get; set; }

    [ObservableProperty]
    public partial string? SetupMessage { get; set; }

    public bool IsSignedIn => State == SessionState.SignedIn;

    public IAuthProvider Auth { get; private set; }

    public DeviceLookup Lookup => new(_graph);

    public DirectoryLookup Directory => new(_graph);

    public DeviceActions DeviceActions => new(_graph);

    public RecoverySecrets RecoverySecrets => new(_graph);

    public AutopilotUpdates AutopilotUpdates => new(_graph);

    public EntraDeviceUpdates EntraDeviceUpdates => new(_graph);

    public SignInLookup SignIns => new(_graph);

    public InstallStatusLookup InstallStatus => new(_graph);

    public GroupMembershipUpdates GroupMembershipUpdates => new(_graph);

    public DeviceInventoryLookup InventoryLookup => new(_graph);

    /// <summary>Rebuilt with the services, so a tenant change never serves the old tenant's cache.</summary>
    public AssignmentCatalog AssignmentCatalog { get; private set; }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (State != SessionState.Restoring) return;
        if (!Auth.IsConfigured)
        {
            State = SessionState.NeedsSetup;
            return;
        }
        if (await Auth.RestoreSessionAsync(cancellationToken) is not { } user)
        {
            State = SessionState.SignedOut;
            return;
        }
        SignedIn(user);
        await LoadTenantNameAsync();
    }

    /// <summary>Saves the tenant setup and rebuilds sign-in against it.</summary>
    public void ApplySetup(AuthConfig config)
    {
        var auth = _makeAuthProvider(config);
        if (!auth.IsConfigured)
        {
            SetupMessage = "Microsoft sign-in couldn't start with these IDs. Check them against the app registration.";
            return;
        }
        AuthConfigStore.Save(config, _store);
        ReplaceServices(auth, _makeGraphClient(auth), config);
        IsDemo = false;
        SetupMessage = null;
        SignInMessage = null;
        State = SessionState.SignedOut;
    }

    /// <summary>Opens tenant setup, signing out first so the old tenant's account isn't left behind.</summary>
    public async Task BeginSetupAsync()
    {
        if (State == SessionState.SignedIn)
        {
            await Auth.SignOutAsync();
            User = null;
        }
        SetupMessage = null;
        State = SessionState.NeedsSetup;
    }

    /// <summary>Leaves setup without changes; only possible when a tenant (or demo) is already set up.</summary>
    public void CancelSetup()
    {
        if (!Auth.IsConfigured) return;
        State = SessionState.SignedOut;
    }

    /// <summary>Switches to sample data and signs straight in.</summary>
    public async Task StartDemoAsync()
    {
        ReplaceServices(new DemoAuthProvider(), new DemoGraphClient(), null);
        IsDemo = true;
        SignInMessage = null;
        await SignInAsync();
    }

    public async Task SignInAsync()
    {
        if (IsSigningIn) return;
        IsSigningIn = true;
        SignInMessage = null;
        try
        {
            SignedIn(await Auth.SignInAsync());
            await LoadTenantNameAsync();
        }
        catch (AuthException error) when (error.Kind == AuthErrorKind.Cancelled)
        {
            // The user backed out; nothing to report.
        }
        catch (AuthException error)
        {
            SignInMessage = error.UserMessage;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            SignInMessage = new AuthException(AuthErrorKind.Failed, error.Message).UserMessage;
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    public async Task SignOutAsync()
    {
        await Auth.SignOutAsync();
        User = null;
        State = SessionState.SignedOut;
    }

    /// <summary>Called by feature view models when Graph reports the session can't be refreshed silently.</summary>
    public void SessionExpired()
    {
        if (State != SessionState.SignedIn) return;
        User = null;
        State = SessionState.SignedOut;
        SignInMessage = GraphException.Unauthorized().UserMessage;
    }

    private void SignedIn(SignedInUser user)
    {
        User = user;
        State = SessionState.SignedIn;
    }

    private void ReplaceServices(IAuthProvider auth, IGraphClient graph, AuthConfig? config)
    {
        Auth = auth;
        _graph = graph;
        Config = config;
        AssignmentCatalog = new AssignmentCatalog(graph);
    }

    private async Task LoadTenantNameAsync()
    {
        if (User is not { TenantName: null } user) return;
        try
        {
            if (await _graph.OrganizationNameAsync() is { } name && State == SessionState.SignedIn)
            {
                User = user with { TenantName = name };
            }
        }
        catch (GraphException)
        {
            // The tenant ID stands in for the name.
        }
    }
}
