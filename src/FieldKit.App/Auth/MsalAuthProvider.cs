using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using FieldKit.Core.Auth;
using FieldKit.Core.Services;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace FieldKit.App.Auth;

/// <summary>
/// Sign-in through MSAL.NET and the Windows account broker (WAM). Tokens live only in MSAL/WAM
/// and are never logged or written anywhere by FieldKit.
/// </summary>
internal sealed class MsalAuthProvider : IAuthProvider
{
    /// <summary>
    /// Set by an explicit sign-out, so the next launch doesn't sign straight back in with the
    /// Windows account; cleared by the next sign-in.
    /// </summary>
    private const string SignedOutKey = "auth.signedOut";

    private readonly IPublicClientApplication? _application;
    private readonly ILocalStore _store;

    public MsalAuthProvider(AuthConfig? config, Func<IntPtr> parentWindow, ILocalStore store)
    {
        _store = store;
        if (config is null) return;
        try
        {
            _application = PublicClientApplicationBuilder.Create(config.ClientId)
                .WithAuthority(config.AuthorityUrl.AbsoluteUri)
                .WithRedirectUri($"ms-appx-web://microsoft.aad.brokerplugin/{config.ClientId}")
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = AppChannel.Name })
                .WithParentActivityOrWindow(parentWindow)
                .Build();
        }
        catch (Exception error) when (error is MsalException or ArgumentException or UriFormatException)
        {
            Debug.WriteLine($"MSAL setup failed: {error.GetType().Name}");
        }
    }

    public bool IsConfigured => _application is not null;

    public async Task<SignedInUser?> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null) return null;
        IAccount? account = null;
        try
        {
            account = await CurrentAccountAsync();
            if (account is not null)
            {
                return User(await Silent(account, forceRefresh: false).ExecuteAsync(cancellationToken));
            }
            if (_store.GetSetting(SignedOutKey) is not null) return null;

            // On an Entra-joined PC, the Windows account signs in silently, but only for its own tenant.
            var result = await Silent(PublicClientApplication.OperatingSystemAccount, forceRefresh: false).ExecuteAsync(cancellationToken);
            if (IsHomeTenant(result)) return User(result);
            await _application.RemoveAsync(result.Account);
            return null;
        }
        catch (Exception error) when (IsNetwork(error) && account is not null)
        {
            // Offline at launch: keep the cached account; Graph calls will retry.
            return new SignedInUser(account.Username, account.Username, account.HomeAccountId?.TenantId ?? "");
        }
        catch (MsalException)
        {
            return null;
        }
    }

    public async Task<SignedInUser> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null) throw new AuthException(AuthErrorKind.NotConfigured);
        try
        {
            var result = await _application.AcquireTokenInteractive(AuthScopes.Graph)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(cancellationToken);
            _store.RemoveSetting(SignedOutKey);
            return User(result);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw Map(error);
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null) return;
        _store.SetSetting(SignedOutKey, "1");
        try
        {
            foreach (var account in await _application.GetAccountsAsync())
            {
                await _application.RemoveAsync(account);
            }
        }
        catch (MsalException error)
        {
            Debug.WriteLine($"Removing the account failed: {error.ErrorCode}");
        }
    }

    public async Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default) =>
        (await SilentForCurrentAccount(forceRefresh, cancellationToken)).AccessToken;

    public async Task<IReadOnlyList<string>> GrantedScopesAsync(bool forceRefresh, CancellationToken cancellationToken = default) =>
        (await SilentForCurrentAccount(forceRefresh, cancellationToken)).Scopes.ToList();

    public async Task<IReadOnlyList<string>> GrantedScopesInteractiveAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null) throw new AuthException(AuthErrorKind.NotConfigured);
        try
        {
            var request = _application.AcquireTokenInteractive(AuthScopes.Graph);
            // The signed-in account, so Windows only asks for what it needs (consent, MFA); no
            // account cached (a Windows-account sign-in) means picking one.
            request = await CurrentAccountAsync() is { } account ? request.WithAccount(account) : request.WithPrompt(Prompt.SelectAccount);
            var result = await request.ExecuteAsync(cancellationToken);
            _store.RemoveSetting(SignedOutKey);
            return result.Scopes.ToList();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw Map(error);
        }
    }

    private async Task<AuthenticationResult> SilentForCurrentAccount(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (_application is null) throw new AuthException(AuthErrorKind.NotConfigured);
        var account = await CurrentAccountAsync() ?? throw new AuthException(AuthErrorKind.NoAccount);
        try
        {
            return await Silent(account, forceRefresh).ExecuteAsync(cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw Map(error);
        }
    }

    private AcquireTokenSilentParameterBuilder Silent(IAccount account, bool forceRefresh) =>
        _application!.AcquireTokenSilent(AuthScopes.Graph, account).WithForceRefresh(forceRefresh);

    private async Task<IAccount?> CurrentAccountAsync() => (await _application!.GetAccountsAsync()).FirstOrDefault();

    /// <summary>The token is for the account's own tenant, not a guest account in the configured one.</summary>
    private static bool IsHomeTenant(AuthenticationResult result) =>
        string.Equals(result.Account?.HomeAccountId?.TenantId, result.TenantId, StringComparison.OrdinalIgnoreCase);

    private static SignedInUser User(AuthenticationResult result)
    {
        var username = result.Account?.Username ?? "";
        var name = result.ClaimsPrincipal?.FindFirst("name")?.Value;
        return new SignedInUser(string.IsNullOrEmpty(name) ? username : name, username, result.TenantId ?? "");
    }

    private static AuthException Map(Exception error) =>
        error switch
        {
            AuthException auth => auth,
            MsalUiRequiredException => new AuthException(AuthErrorKind.InteractionRequired),
            MsalClientException { ErrorCode: MsalError.AuthenticationCanceledError } => new AuthException(AuthErrorKind.Cancelled),
            _ when IsNetwork(error) => new AuthException(AuthErrorKind.Network),
            MsalException msal => new AuthException(AuthErrorKind.Failed, msal.Message),
            _ => new AuthException(AuthErrorKind.Failed, error.Message),
        };

    private static bool IsNetwork(Exception error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or SocketException) return true;
            if (current is MsalClientException { ErrorCode: "request_timeout" or "network_not_available" }) return true;
        }
        return false;
    }
}
