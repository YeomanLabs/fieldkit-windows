namespace FieldKit.Core.Tests;

/// <summary>
/// Ports AuthConfigTests.swift. The store tests use the SQLite settings table that replaces
/// UserDefaults; Windows has no build-time prefill, so "prefers saved over bundle" is just a round trip.
/// </summary>
public sealed class AuthConfigTests
{
    private const string Client = "1A2B3C4D-1111-2222-3333-444455556666";
    private const string Tenant = "9f8e7d6c-aaaa-bbbb-cccc-ddddeeeeffff";

    [Fact]
    public void AuthorityUsesTenant()
    {
        var config = new AuthConfig("c", "contoso.onmicrosoft.com");
        Assert.Equal("https://login.microsoftonline.com/contoso.onmicrosoft.com", config.AuthorityUrl.AbsoluteUri);
    }

    [Fact]
    public void SignInUsesTheRegistrationsGrantedPermissions()
    {
        Assert.Equal(["https://graph.microsoft.com/.default"], AuthScopes.Graph);
    }

    [Fact]
    public void WriteAccessIsLimitedToPhaseTwoAndThree()
    {
        // Guard: only Autopilot pre-staging, device actions, group membership and (optionally,
        // approved 2026-10-13) device extension attributes may write. Adding another write or
        // wipe-style permission should be a deliberate roadmap change.
        var writes = GraphPermission.All
            .Select(permission => permission.Name)
            // Reads have a plain "Read" segment: User.Read, Device.Read.All, Policy.Read.ConditionalAccess.
            .Where(name => !name.Split('.').Contains("Read"))
            .ToHashSet();
        Assert.Equal(
            new HashSet<string>
            {
                "DeviceManagementServiceConfig.ReadWrite.All",
                "DeviceManagementManagedDevices.PrivilegedOperations.All",
                "GroupMember.ReadWrite.All",
                "Directory.AccessAsUser.All",
            },
            writes);
    }

    [Fact]
    public void EveryFeatureNamesARegisteredPermission()
    {
        var registered = GraphPermission.All.Select(permission => permission.Name).ToHashSet();
        foreach (var feature in Enum.GetValues<GraphFeature>())
        {
            Assert.Contains(feature.Permission(), registered);
            Assert.Contains(feature.Permission(), feature.Message(GraphException.Forbidden()), StringComparison.Ordinal);
            Assert.Equal(GraphException.Network().UserMessage, feature.Message(GraphException.Network()));
        }
    }

    [Fact]
    public void AcceptsGuidsAndTrimsPastedWhitespace()
    {
        var (config, problem) = AuthConfig.Validated($"  {Client}\n", $" {Tenant} ");
        Assert.Null(problem);
        Assert.Equal(Client.ToLowerInvariant(), config!.ClientId);
        Assert.Equal(Tenant, config.TenantId);
    }

    [Theory]
    [InlineData("contoso.onmicrosoft.com")]
    [InlineData("Contoso.co.uk")]
    [InlineData("it-dept.example.com")]
    public void AcceptsTenantDomains(string domain)
    {
        var (config, problem) = AuthConfig.Validated(Client, domain);
        Assert.Null(problem);
        Assert.Equal(domain.ToLowerInvariant(), config!.TenantId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("YOUR_CLIENT_ID")]
    [InlineData("$(MSAL_CLIENT_ID)")]
    public void RejectsBadClientIds(string value)
    {
        Assert.Equal(AuthConfigProblem.ClientIdInvalid, AuthConfig.Validated(value, Tenant).Problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("contoso")]
    [InlineData("https://login.microsoftonline.com/x")]
    [InlineData("-bad.com")]
    [InlineData("YOUR_TENANT_ID")]
    public void RejectsBadTenants(string value)
    {
        Assert.Equal(AuthConfigProblem.TenantIdInvalid, AuthConfig.Validated(Client, value).Problem);
    }

    [Theory]
    [InlineData("common")]
    [InlineData("Organizations")]
    [InlineData("consumers")]
    public void RejectsMultiTenantAuthorities(string value)
    {
        Assert.Equal(AuthConfigProblem.MultiTenantAuthority, AuthConfig.Validated(Client, value).Problem);
    }

    [Fact]
    public void StoreRoundTrips()
    {
        using var store = Persistence.SqliteLocalStore.OpenInMemory();
        Assert.Null(Persistence.AuthConfigStore.Saved(store));

        var config = AuthConfig.Validated(Client, Tenant).Config!;
        Persistence.AuthConfigStore.Save(config, store);
        Assert.Equal(config, Persistence.AuthConfigStore.Saved(store));

        Persistence.AuthConfigStore.Clear(store);
        Assert.Null(Persistence.AuthConfigStore.Saved(store));
    }

    [Fact]
    public void StoreIgnoresSavedValuesThatNoLongerValidate()
    {
        using var store = Persistence.SqliteLocalStore.OpenInMemory();
        Persistence.AuthConfigStore.Save(new AuthConfig("nope", "common"), store);
        Assert.Null(Persistence.AuthConfigStore.Saved(store));
    }
}
