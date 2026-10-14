using FieldKit.Core.Services;

namespace FieldKit.Core.Persistence;

public static class LocalDataCleaner
{
    /// <summary>
    /// Deletes recent lookups, site walks and settings. Tenant setup is kept;
    /// sign-in state belongs to MSAL/WAM and is cleared by signing out.
    /// </summary>
    public static void ClearAll(ILocalStore store, SettingsStore settings)
    {
        store.ClearLocalData(AuthConfigStore.Keys);
        settings.ResetAll();
    }
}
