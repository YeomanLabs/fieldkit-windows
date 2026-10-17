using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>
/// New for Windows (2026-10-16): on a real tenant, remediations with <c>$expand=assignments</c>
/// answered a server error every time, so the whole Remediations section failed. The catalog
/// now reads each item's assignments on its own instead.
/// </summary>
public sealed class AssignmentCatalogFallbackTests
{
    private const string GroupAssignment =
        """{"value":[{"id":"a-1","target":{"@odata.type":"#microsoft.graph.groupAssignmentTarget","groupId":"g-1"}}]}""";

    private static bool Expanded(GraphRequest request) => Uri.UnescapeDataString(request.Url.Query).Contains("$expand=assignments", StringComparison.Ordinal);

    /// <summary>Graph as seen on that tenant: the expanded list fails; one item's own assignments fail too.</summary>
    private static ScriptedGraphClient Tenant(int expandedStatus = 500) => new(request =>
    {
        var path = request.Url.AbsolutePath;
        if (path.EndsWith("/deviceHealthScripts", StringComparison.Ordinal))
        {
            return Expanded(request)
                ? throw GraphException.Server(expandedStatus, null, "11111111-2222-3333-4444-555555555555")
                : ScriptedGraphClient.Page(["Clear Teams cache", "Fix VPN", "Old fix"]);
        }
        if (path.EndsWith("/clear%20teams%20cache/assignments", StringComparison.Ordinal)) return GroupAssignment;
        if (path.EndsWith("/fix%20vpn/assignments", StringComparison.Ordinal)) return """{"value":[]}""";
        if (path.EndsWith("/old%20fix/assignments", StringComparison.Ordinal)) throw GraphException.Server(500, null);
        throw GraphException.NotFound();
    });

    [Fact]
    public async Task AServerErrorOnTheExpandedListReadsEachItemsAssignments()
    {
        var client = Tenant();
        var items = await new AssignmentCatalog(client).ItemsAsync(AssignmentKind.Remediation);

        Assert.Equal(["Clear Teams cache", "Fix VPN", "Old fix"], items.Select(item => item.Name));
        Assert.Equal(AssignmentTarget.Group("g-1"), Assert.Single(items[0].Assignments).Target);
        Assert.Empty(items[1].Assignments);
        Assert.False(items[1].AssignmentsUnread);
        Assert.True(items[2].AssignmentsUnread);

        var paths = client.Sent.Select(request => Uri.UnescapeDataString(request.Url.PathAndQuery)).ToList();
        Assert.Contains(paths, path => path.Contains("$expand=assignments", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.EndsWith("deviceHealthScripts?$select=id,displayName,publisher", StringComparison.Ordinal));
        Assert.Equal(3, paths.Count(path => path.EndsWith("/assignments", StringComparison.Ordinal)));
        Assert.All(client.Sent, request => Assert.StartsWith("https://graph.microsoft.com/beta/", request.Url.AbsoluteUri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheGroupPageShowsWhatItCouldReadAndNamesWhatItCouldnt()
    {
        var group = new DirectoryGroup { Id = "g-1", DisplayName = "Intune Users - Contoso HQ" };
        var model = new GroupViewModel(group, new AssignmentCatalog(Tenant()), () => { });
        await model.LoadAsync();

        var remediations = model.Sections.Single(section => section.Kind == AssignmentKind.Remediation);
        Assert.Equal(SectionKind.Loaded, remediations.State.Kind);
        Assert.Equal("Clear Teams cache", Assert.Single(remediations.Items).Item.Name);
        Assert.Equal("Graph wouldn't give the assignments of Old fix, so it may be missing here.", remediations.Note);
    }

    [Fact]
    public void TheNoteNamesAFewThenCounts()
    {
        var items = Enumerable.Range(1, 7)
            .Select(number => new AssignedItem { Id = $"r{number}", Name = $"Fix {number}", AssignmentsUnread = true })
            .Append(new AssignedItem { Id = "ok", Name = "Fine" })
            .ToList();
        Assert.Equal(
            "Graph wouldn't give the assignments of Fix 1, Fix 2, Fix 3, Fix 4, Fix 5 and 2 more, so they may be missing here.",
            AssignmentKindSection.UnreadNote(items));
        Assert.Equal("", AssignmentKindSection.UnreadNote([new AssignedItem { Id = "ok", Name = "Fine" }]));
    }

    [Fact]
    public async Task AccessDeniedStillFailsTheSectionWithoutTheFallback()
    {
        var client = new ScriptedGraphClient(_ => throw GraphException.Forbidden());
        var error = await Assert.ThrowsAsync<GraphException>(() => new AssignmentCatalog(client).ItemsAsync(AssignmentKind.Remediation));
        Assert.Equal(GraphErrorKind.Forbidden, error.Kind);
        Assert.Single(client.Sent);
    }

    [Fact]
    public async Task WhenTheExpandedListWorksNothingElseIsAsked()
    {
        var client = new ScriptedGraphClient(_ =>
            """{"value":[{"id":"r1","displayName":"Fix VPN","assignments":[{"id":"a-1","target":{"@odata.type":"#microsoft.graph.allDevicesAssignmentTarget"}}]}]}""");
        var items = await new AssignmentCatalog(client).ItemsAsync(AssignmentKind.Remediation);
        Assert.Equal(AssignmentTarget.AllDevices, Assert.Single(Assert.Single(items).Assignments).Target);
        Assert.Single(client.Sent);
    }

    [Fact]
    public void ServerErrorsCarryTheStatusAndRequestId()
    {
        var error = GraphException.Server(500, "InternalServerError", "11111111-2222-3333-4444-555555555555");
        Assert.Equal("Microsoft Graph had a problem (HTTP 500, request ID 11111111-2222-3333-4444-555555555555). Retry in a moment.", error.Message);
        Assert.True(error.Matches(GraphException.Server(500, "InternalServerError")));
        Assert.Equal("Microsoft Graph had a problem (HTTP 504). Retry in a moment.", GraphException.Server(504, null).Message);
    }
}
