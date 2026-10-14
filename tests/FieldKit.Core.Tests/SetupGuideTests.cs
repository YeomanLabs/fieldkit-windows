using FieldKit.Core.Setup;

namespace FieldKit.Core.Tests;

/// <summary>Ports SetupGuideTests.swift for the Windows guide (broker redirect URI instead of a bundle ID).</summary>
public sealed class SetupGuideTests
{
    private const string ClientId = "1a2b3c4d-1111-2222-3333-444455556666";

    [Fact]
    public void RedirectUriUsesTheTypedClientId()
    {
        var guide = new SetupGuide("  1A2B3C4D-1111-2222-3333-444455556666 ");
        Assert.Equal($"ms-appx-web://microsoft.aad.brokerplugin/{ClientId}", guide.RedirectUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1a2b3c4d-11")]
    public void RedirectUriShowsAPlaceholderUntilTheIdIsValid(string? typed)
    {
        Assert.Equal("ms-appx-web://microsoft.aad.brokerplugin/<client ID>", new SetupGuide(typed).RedirectUri);
    }

    [Fact]
    public void ListsEveryPermissionTheAppNeeds()
    {
        Assert.Equal(GraphPermission.All.Select(permission => permission.Name), SetupGuide.ScopeItems.Select(item => item.Value));
        // iOS's 13, plus Windows' optional Directory.AccessAsUser.All for extension attributes and the
        // two sign-in log reads (AuditLog.Read.All, Policy.Read.ConditionalAccess).
        Assert.Equal(16, SetupGuide.ScopeItems.Count);
    }

    [Fact]
    public void PlainTextCarriesEverythingAnAdminNeeds()
    {
        var text = new SetupGuide(ClientId).PlainText;
        Assert.Contains($"ms-appx-web://microsoft.aad.brokerplugin/{ClientId}", text, StringComparison.Ordinal);
        Assert.Contains("Mobile and desktop applications", text, StringComparison.Ordinal);
        foreach (var scope in SetupGuide.ScopeItems)
        {
            Assert.Contains(scope.Value, text, StringComparison.Ordinal);
        }
        Assert.Contains("Grant admin consent", text, StringComparison.Ordinal);
        Assert.Contains("No client secret", text, StringComparison.Ordinal);
        Assert.Contains("entra.microsoft.com", text, StringComparison.Ordinal);
        Assert.Contains(SetupGuide.ExistingRegistrationNote, text, StringComparison.Ordinal);
    }
}
