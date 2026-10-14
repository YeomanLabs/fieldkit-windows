using FieldKit.Core.Demo;
using FieldKit.Core.Persistence;
using FieldKit.Core.Services;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows (iOS has no AppSession tests): setup, sign-in, demo and expiry.</summary>
public sealed class AppSessionTests : IDisposable
{
    private static readonly AuthConfig Config = new("1a2b3c4d-1111-2222-3333-444455556666", "contoso.onmicrosoft.com");

    private readonly SqliteLocalStore _store = SqliteLocalStore.OpenInMemory();
    private readonly FakeAuthProvider _auth = new();

    public void Dispose() => _store.Dispose();

    private AppSession Session(Func<AuthConfig?, IAuthProvider>? makeAuth = null, bool demo = false) =>
        new(_store, makeAuth ?? (config => config is null ? new FakeAuthProvider { IsConfigured = false } : _auth),
            _ => new DemoGraphClient(TimeSpan.Zero), demo);

    [Fact]
    public async Task WithoutSetupItAsksForSetup()
    {
        var session = Session();
        await session.RestoreAsync();
        Assert.Equal(SessionState.NeedsSetup, session.State);
        Assert.Null(session.Config);
    }

    [Fact]
    public async Task SavedSetupRestoresTheCachedAccount()
    {
        AuthConfigStore.Save(Config, _store);
        _auth.CachedUser = FakeAuthProvider.Alex;
        var session = Session();
        await session.RestoreAsync();
        Assert.Equal(SessionState.SignedIn, session.State);
        Assert.Equal(Config, session.Config);
        // The tenant name comes from Graph after sign-in.
        Assert.Equal("Contoso (demo)", session.User?.TenantName);
    }

    [Fact]
    public async Task SavedSetupWithoutAnAccountShowsSignIn()
    {
        AuthConfigStore.Save(Config, _store);
        var session = Session();
        await session.RestoreAsync();
        Assert.Equal(SessionState.SignedOut, session.State);
    }

    [Fact]
    public void ApplyingSetupSavesItAndGoesToSignIn()
    {
        var session = Session();
        session.ApplySetup(Config);
        Assert.Equal(SessionState.SignedOut, session.State);
        Assert.Equal(Config, AuthConfigStore.Saved(_store));
        Assert.False(session.IsDemo);
    }

    [Fact]
    public void SetupThatMsalRejectsIsNotSaved()
    {
        var session = Session(_ => new FakeAuthProvider { IsConfigured = false });
        session.ApplySetup(Config);
        Assert.NotNull(session.SetupMessage);
        Assert.Null(AuthConfigStore.Saved(_store));
        Assert.Equal(SessionState.Restoring, session.State);
    }

    [Fact]
    public async Task SignInFailuresExplainButCancellingIsQuiet()
    {
        AuthConfigStore.Save(Config, _store);
        var session = Session();
        await session.RestoreAsync();

        _auth.SignInError = new AuthException(AuthErrorKind.Cancelled);
        await session.SignInAsync();
        Assert.Null(session.SignInMessage);
        Assert.Equal(SessionState.SignedOut, session.State);

        _auth.SignInError = new AuthException(AuthErrorKind.Network);
        await session.SignInAsync();
        Assert.Equal(new AuthException(AuthErrorKind.Network).UserMessage, session.SignInMessage);
        Assert.False(session.IsSigningIn);

        _auth.SignInError = null;
        await session.SignInAsync();
        Assert.Equal(SessionState.SignedIn, session.State);
        Assert.Null(session.SignInMessage);
    }

    [Fact]
    public async Task DemoSignsStraightIn()
    {
        var session = Session();
        await session.RestoreAsync();
        await session.StartDemoAsync();
        Assert.True(session.IsDemo);
        Assert.Equal(SessionState.SignedIn, session.State);
        Assert.Equal("Demo Engineer", session.User?.DisplayName);
        Assert.Null(session.Config);
    }

    [Fact]
    public async Task DemoLaunchArgumentStartsAtEnterDemo()
    {
        var session = Session(demo: true);
        await session.RestoreAsync();
        Assert.True(session.IsDemo);
        Assert.Equal(SessionState.SignedOut, session.State);
        await session.SignInAsync();
        Assert.Equal(SessionState.SignedIn, session.State);
    }

    [Fact]
    public async Task ChangingSetupSignsOutFirst()
    {
        AuthConfigStore.Save(Config, _store);
        _auth.CachedUser = FakeAuthProvider.Alex;
        var session = Session();
        await session.RestoreAsync();

        await session.BeginSetupAsync();
        Assert.Equal(SessionState.NeedsSetup, session.State);
        Assert.Equal(1, _auth.SignOuts);
        Assert.Null(session.User);

        session.CancelSetup();
        Assert.Equal(SessionState.SignedOut, session.State);
    }

    [Fact]
    public async Task ExpiredSessionReturnsToSignInWithAReason()
    {
        AuthConfigStore.Save(Config, _store);
        _auth.CachedUser = FakeAuthProvider.Alex;
        var session = Session();
        await session.RestoreAsync();

        session.SessionExpired();
        Assert.Equal(SessionState.SignedOut, session.State);
        Assert.Equal(GraphException.Unauthorized().UserMessage, session.SignInMessage);

        // Only a signed-in session can expire.
        session.SignInMessage = null;
        session.SessionExpired();
        Assert.Null(session.SignInMessage);
    }

    [Fact]
    public async Task SigningOutClearsTheUser()
    {
        AuthConfigStore.Save(Config, _store);
        _auth.CachedUser = FakeAuthProvider.Alex;
        var session = Session();
        await session.RestoreAsync();
        await session.SignOutAsync();
        Assert.Equal(SessionState.SignedOut, session.State);
        Assert.Null(session.User);
        Assert.Equal(1, _auth.SignOuts);
    }
}
