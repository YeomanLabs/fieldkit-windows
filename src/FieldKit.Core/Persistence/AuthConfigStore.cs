using FieldKit.Core.Auth;
using FieldKit.Core.Services;

namespace FieldKit.Core.Persistence;

/// <summary>
/// Tenant setup entered in the app, in the settings table. These are identifiers, not
/// secrets; tokens stay in MSAL/WAM.
/// </summary>
public static class AuthConfigStore
{
    public const string TenantIdKey = "auth.tenantId";
    public const string ClientIdKey = "auth.clientId";

    /// <summary>The settings "Clear all local data" keeps.</summary>
    public static IReadOnlyCollection<string> Keys { get; } = [TenantIdKey, ClientIdKey];

    public static AuthConfig? Saved(ILocalStore store)
    {
        if (store.GetSetting(TenantIdKey) is not { } tenantId || store.GetSetting(ClientIdKey) is not { } clientId) return null;
        // Re-validate in case the rules tightened since it was saved.
        return AuthConfig.Validated(clientId, tenantId).Config;
    }

    public static void Save(AuthConfig config, ILocalStore store)
    {
        store.SetSetting(TenantIdKey, config.TenantId);
        store.SetSetting(ClientIdKey, config.ClientId);
    }

    public static void Clear(ILocalStore store)
    {
        store.RemoveSetting(TenantIdKey);
        store.RemoveSetting(ClientIdKey);
    }
}
