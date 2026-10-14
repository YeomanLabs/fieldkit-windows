using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Persistence;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;

namespace FieldKit.Core.Tests;

/// <summary>Ports the pre-stage and Autopilot edit tests from <c>PhaseTwoThreeModelTests</c>, plus pasted lists.</summary>
public sealed class PreStageTests : IDisposable
{
    private const string Identities = "deviceManagement/windowsAutopilotDeviceIdentities";
    private readonly SqliteLocalStore _store = SqliteLocalStore.OpenInMemory();

    public void Dispose() => _store.Dispose();

    private SettingsStore Settings() => new(_store);

    private static PreStageViewModel PreStage(StubGraphClient stub, SettingsStore settings) =>
        new(new DeviceLookup(stub), new AutopilotUpdates(stub), settings, () => { });

    [Fact]
    public async Task PreStageTagsUntaggedRecordsAndSkipsDuplicates()
    {
        var stub = new StubGraphClient(new()
        {
            [Identities] = "autopilot_only",
            [$"{Identities}/ap-0100/updateDeviceProperties"] = "",
        });
        var settings = Settings();
        var model = PreStage(stub, settings);
        model.GroupTagText = " CTS-Kiosk ";
        var first = model.Add("PF9NEW01");
        await model.Add("PF9NEW01");
        Assert.Single(model.Results);
        Assert.Equal("PF9NEW01 already scanned", model.Banner?.Text);

        await first;
        Assert.Equal(new PreStageStatus.Tagged("CTS-Kiosk"), model.Results[0].Status);
        Assert.Equal(["CTS-Kiosk"], settings.RecentGroupTags);
        Assert.Equal("ThinkPad T14 Gen 5", model.Results[0].Model);
        Assert.Equal("Scanned (1, 1 tagged)", model.ResultsHeader);
    }

    [Fact]
    public async Task PreStageNeedsATagAndReportsMissingRecords()
    {
        var stub = new StubGraphClient(new() { [Identities] = "autopilot_empty" });
        var model = PreStage(stub, Settings());
        model.GroupTagText = "";
        await model.Add("BOX00001");
        Assert.Empty(model.Results);
        Assert.True(model.Banner?.IsWarning);

        model.GroupTagText = "CTS-Standard";
        await model.Add("BOX00001");
        Assert.Equal(PreStageStatus.NotInAutopilot, model.Results[0].Status);
        Assert.True(model.Results[0].IsWarning);
    }

    [Fact]
    public async Task AlreadyTaggedRecordsAreLeftAlone()
    {
        var stub = new StubGraphClient(new() { [Identities] = "autopilot_only" });
        var model = PreStage(stub, Settings());
        model.GroupTagText = "CTS-Standard";
        await model.Add("PF9NEW01");
        Assert.Equal(new PreStageStatus.AlreadyTagged("CTS-Standard"), model.Results[0].Status);
        Assert.DoesNotContain(stub.Sent, request => request.Method == GraphMethod.Post);
        Assert.Equal("Scanned (1, 0 tagged)", model.ResultsHeader);
    }

    [Fact]
    public async Task FailuresNameThePermissionAndCanBeRetried()
    {
        var replies = new Queue<StubReply>([GraphException.Forbidden(), ""]);
        var stub = new StubGraphClient(new() { [Identities] = "autopilot_only" }, request =>
            request.Url.AbsolutePath.EndsWith("/updateDeviceProperties", StringComparison.Ordinal) ? replies.Dequeue() : null);
        var model = PreStage(stub, Settings());
        model.GroupTagText = "CTS-Kiosk";
        await model.Add("PF9NEW01");
        Assert.Equal(new PreStageStatus.Failed(GraphFeature.AutopilotEdit.AccessDeniedMessage()), model.Results[0].Status);
        Assert.True(model.Results[0].IsFailed);

        await model.Retry(model.Results[0]);
        Assert.Equal(new PreStageStatus.Tagged("CTS-Kiosk"), model.Results[0].Status);
    }

    [Fact]
    public async Task APastedListIsTaggedInOrderSkippingDuplicatesAndJunk()
    {
        var stub = new StubGraphClient(new() { [Identities] = "autopilot_empty" });
        var model = PreStage(stub, Settings());
        model.GroupTagText = "CTS-Standard";
        await model.Add("BOX00002");

        await model.AddListAsync("box00001\r\nBOX00002\tBOX00003, box00001\nx\n\n");

        // Newest first, so the list's order reads bottom-up.
        Assert.Equal(["BOX00003", "BOX00001", "BOX00002"], model.Results.Select(result => result.Serial));
        var lookups = stub.Sent.Select(request => request.Url.Query).ToList();
        Assert.Equal(3, lookups.Count);
        Assert.Contains("BOX00001", Uri.UnescapeDataString(lookups[1]), StringComparison.Ordinal);
        Assert.Contains("BOX00003", Uri.UnescapeDataString(lookups[2]), StringComparison.Ordinal);
        Assert.Equal("Added 2 serials. 1 was already in the list. Skipped 1 that doesn't look like serials: x", model.Banner?.Text);
        Assert.True(model.Banner?.IsWarning);
    }

    [Fact]
    public void SerialListSplitsLinesCommasAndTabs()
    {
        var list = SerialList.Parse(" 5cg1234xyz \r\nPF2ABCDE,PF9NEW01\tpf2abcde;;too long serial number that is not one\n");
        Assert.Equal(["5CG1234XYZ", "PF2ABCDE", "PF9NEW01"], list.Serials);
        Assert.Equal(["too long serial number that is not one"], list.Invalid);
        Assert.Empty(SerialList.Parse("  \n\t ,").Serials);
    }

    [Fact]
    public async Task AutopilotEditSendsOnlyWhatChanged()
    {
        const string Base = $"{Identities}/ap-1";
        var stub = new StubGraphClient(new()
        {
            [$"{Base}/updateDeviceProperties"] = "",
            [$"{Base}/unassignUserFromDevice"] = "",
        });
        var record = new AutopilotDevice { Id = "ap-1", SerialNumber = "S", GroupTag = "Old", AssignedUser = "a@b.com" };
        var settings = Settings();
        var model = new AutopilotEditViewModel(record, new AutopilotUpdates(stub), settings, () => { });
        Assert.False(model.HasChanges);
        model.UserChange = AutopilotUserChange.Remove;
        Assert.Equal("None (will be removed)", model.AssignedUserText);
        Assert.True(await model.SaveAsync());
        Assert.Equal(["unassignUserFromDevice"], stub.Sent.Select(request => request.Url.Segments[^1]));

        model.UserChange = AutopilotUserChange.Keep;
        model.GroupTagText = "New";
        Assert.True(await model.SaveAsync());
        Assert.Equal("updateDeviceProperties", stub.Sent[^1].Url.Segments[^1]);
        Assert.Equal(["New"], settings.RecentGroupTags);
    }

    [Fact]
    public async Task AutopilotEditExplainsABadTagWithoutCallingGraph()
    {
        var stub = new StubGraphClient([]);
        var record = new AutopilotDevice { Id = "ap-1", GroupTag = "Old" };
        var model = new AutopilotEditViewModel(record, new AutopilotUpdates(stub), Settings(), () => { });
        model.GroupTagText = "line\nbreak";
        Assert.True(model.HasChanges);
        Assert.False(await model.SaveAsync());
        Assert.Equal("Group tags can be up to 100 characters, without line breaks.", model.ErrorMessage);
        Assert.Empty(stub.Sent);
        Assert.Equal("Assign a user", model.ChangeUserText);
        Assert.False(model.ShowsRemoveToggle);
    }
}
