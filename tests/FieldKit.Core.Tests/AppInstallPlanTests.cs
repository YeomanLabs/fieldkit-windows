namespace FieldKit.Core.Tests;

/// <summary>Ports AppInstallTests.swift's <c>AppInstallPlanTests</c>.</summary>
public sealed class AppInstallPlanTests
{
    private static AssignedItem App(IReadOnlyList<AssignmentEntry> entries, string type = "win32LobApp") =>
        new() { Id = "a", Kind = AssignmentKind.App, Name = "App", Assignments = entries, TypeName = type };

    private static Dictionary<string, string> Groups(params (string Id, string Name)[] groups) =>
        groups.ToDictionary(group => group.Id, group => group.Name);

    private static void AssertStatus(AppInstallPlan.StatusKind kind, string[] via, AppInstallPlan.Status status)
    {
        Assert.Equal(kind, status.Kind);
        Assert.Equal(via, status.Via);
    }

    [Theory]
    [InlineData("win32LobApp", AppPlatform.Windows)]
    [InlineData("winGetApp", AppPlatform.Windows)]
    [InlineData("officeSuiteApp", AppPlatform.Windows)]
    [InlineData("windowsMicrosoftEdgeApp", AppPlatform.Windows)]
    [InlineData("microsoftStoreForBusinessApp", AppPlatform.Windows)]
    [InlineData("iosVppApp", AppPlatform.iOS)]
    [InlineData("managedIOSStoreApp", AppPlatform.iOS)]
    [InlineData("androidManagedStoreApp", AppPlatform.Android)]
    [InlineData("managedAndroidStoreApp", AppPlatform.Android)]
    [InlineData("macOSDmgApp", AppPlatform.macOS)]
    public void AppTypesMapToPlatforms(string type, AppPlatform platform)
    {
        Assert.Equal(platform, AppPlatformParser.FromAppType(type));
        Assert.Equal(platform, AppPlatformParser.FromAppType($"#microsoft.graph.{type}"));
    }

    [Fact]
    public void CrossPlatformAndUnknownTypesHaveNoPlatform()
    {
        Assert.Null(AppPlatformParser.FromAppType("webApp"));
        Assert.Null(AppPlatformParser.FromAppType(""));
        Assert.Equal(AppPlatform.Windows, AppPlatformParser.FromOperatingSystem("Windows"));
        Assert.Equal(AppPlatform.iOS, AppPlatformParser.FromOperatingSystem("iPadOS"));
        Assert.Null(AppPlatformParser.FromOperatingSystem("Linux"));
        var policy = new AssignedItem { Id = "p", Kind = AssignmentKind.CompliancePolicy, Name = "", TypeName = "win32LobApp" };
        Assert.Null(policy.Platform);
    }

    [Fact]
    public void StatusOnlyCountsRequired()
    {
        var available = App([new AssignmentEntry(AssignmentTarget.Group("g1"), "available")]);
        AssertStatus(AppInstallPlan.StatusKind.NotRequired, [],
            AppInstallPlan.StatusOf(available, Groups(("g1", "One")), Groups(), hasUser: false));

        var required = App([new AssignmentEntry(AssignmentTarget.Group("g1"), "required")]);
        AssertStatus(AppInstallPlan.StatusKind.Required, ["One"],
            AppInstallPlan.StatusOf(required, Groups(("g1", "One")), Groups(), hasUser: false));
        AssertStatus(AppInstallPlan.StatusKind.Required, ["One"],
            AppInstallPlan.StatusOf(required, Groups(), Groups(("g1", "One")), hasUser: true));
        AssertStatus(AppInstallPlan.StatusKind.NotRequired, [],
            AppInstallPlan.StatusOf(required, Groups(), Groups(), hasUser: true));
    }

    [Fact]
    public void AllUsersOnlyCountsWithAUser()
    {
        var everyone = App([new AssignmentEntry(AssignmentTarget.AllUsers, "required")]);
        AssertStatus(AppInstallPlan.StatusKind.NotRequired, [],
            AppInstallPlan.StatusOf(everyone, Groups(), Groups(), hasUser: false));
        AssertStatus(AppInstallPlan.StatusKind.Required, ["All users"],
            AppInstallPlan.StatusOf(everyone, Groups(), Groups(), hasUser: true));
        Assert.Equal((false, true), AppInstallPlan.RequiredForAll(everyone));
    }

    [Fact]
    public void ExclusionWins()
    {
        var excluded = App(
        [
            new AssignmentEntry(AssignmentTarget.AllDevices, "required"),
            new AssignmentEntry(AssignmentTarget.ExcludedGroup("kiosk")),
        ]);
        AssertStatus(AppInstallPlan.StatusKind.Excluded, ["Kiosk"],
            AppInstallPlan.StatusOf(excluded, Groups(("kiosk", "Kiosk")), Groups(), hasUser: false));
    }

    [Fact]
    public void RequiredGroupsAreUniqueAndRequiredOnly()
    {
        var mixed = App(
        [
            new AssignmentEntry(AssignmentTarget.Group("g1"), "required"),
            new AssignmentEntry(AssignmentTarget.Group("g2"), "available"),
            new AssignmentEntry(AssignmentTarget.Group("g1"), "required"),
            new AssignmentEntry(AssignmentTarget.Group("g3"), "Required"),
            new AssignmentEntry(AssignmentTarget.ExcludedGroup("g4"), "required"),
            new AssignmentEntry(AssignmentTarget.AllDevices, "required"),
        ]);
        Assert.Equal(["g1", "g3"], AppInstallPlan.RequiredGroupIds(mixed));
    }
}
