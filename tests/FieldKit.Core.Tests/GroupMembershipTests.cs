using FieldKit.Core.Tests.Support;

namespace FieldKit.Core.Tests;

/// <summary>
/// The Graph-level tests from AppInstallTests.swift's <c>InstallAppModelTests</c>. The view model
/// tests in that suite are ported with the Install app feature (M5).
/// </summary>
public sealed class GroupMembershipTests
{
    [Fact]
    public async Task MembershipAddPostsTheObjectReference()
    {
        var stub = new StubGraphClient(new() { ["groups/g-7zip/members/$ref"] = "" });
        await new GroupMembershipUpdates(stub).AddMemberAsync("obj-1", "g-7zip");
        var request = Assert.Single(stub.Sent);
        Assert.Equal(GraphMethod.Post, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/groups/g-7zip/members/$ref", request.Url.AbsoluteUri);
        Assert.Equal(
            new Dictionary<string, string> { ["@odata.id"] = "https://graph.microsoft.com/v1.0/directoryObjects/obj-1" },
            RequestInspection.JsonBody(request.Body));
    }

    [Fact]
    public async Task RemoveSendsDeleteForTheDirectoryObject()
    {
        var stub = new StubGraphClient(new() { ["groups/g-7zip/members/obj-1/$ref"] = "" });
        await new GroupMembershipUpdates(stub).RemoveMemberAsync("obj-1", "g-7zip");
        var request = Assert.Single(stub.Sent);
        Assert.Equal(GraphMethod.Delete, request.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/groups/g-7zip/members/obj-1/$ref", request.Url.AbsoluteUri);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task DirectMembershipComesFromMemberOf()
    {
        var stub = new StubGraphClient(new() { ["devices/obj-1/memberOf"] = "member_of" });
        var groups = await new DirectoryLookup(stub).GroupsOfAsync("devices/obj-1", transitive: false);
        Assert.Equal(2, groups.Count);
        Assert.EndsWith("/memberOf", stub.Requests[0].AbsolutePath, StringComparison.Ordinal);
    }
}
