using FieldKit.Core.Demo;
using FieldKit.Core.Tests.Support;
using FieldKit.Core.ViewModels;
using static FieldKit.Core.Tests.Support.ScriptedTransport;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows (2026-10-14): "why was I blocked?" from a person's sign-in logs.</summary>
public sealed class SignInTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 14, 0, 0, TimeSpan.Zero);
    private int _expired;

    private static async Task<IReadOnlyList<SignIn>> Fixture()
    {
        var stub = new StubGraphClient(new() { ["auditLogs/signIns"] = "signIns" });
        return await new SignInLookup(stub, new FixedTime(Now)).RecentAsync("u-casey");
    }

    private static SignIn With(int error, ConditionalAccessStatus ca, IReadOnlyList<SignInPolicy>? policies, SignInDevice? device = null, string client = "Browser") =>
        new() { Id = "req-1", AppName = "Microsoft Teams", ErrorCode = error, ConditionalAccess = ca, Policies = policies, Device = device ?? new SignInDevice(), ClientApp = client, FailureReason = "Entra's own words." };

    private static SignInPolicy Policy(string name, SignInPolicyResult result, params string[] grants) => new("id-" + name, name, result, grants, []);

    [Fact]
    public async Task DecodesSignInsWithPoliciesDevicesAndLocations()
    {
        var signIns = await Fixture();
        Assert.Equal(3, signIns.Count);

        var blocked = signIns[0];
        Assert.Equal(53000, blocked.ErrorCode);
        Assert.Equal(ConditionalAccessStatus.Failure, blocked.ConditionalAccess);
        Assert.Equal("Microsoft Teams", blocked.AppName);
        Assert.Equal("Redmond, Washington, US", blocked.Location);
        Assert.Equal(("a0000000-0000-4000-8000-000000000006", "CTS-LT-01188", false, "Azure AD joined"),
            (blocked.Device.DeviceId, blocked.Device.DisplayName, blocked.Device.IsCompliant, blocked.Device.TrustType));
        var failed = Assert.Single(blocked.Policies!, policy => policy.Failed);
        Assert.Equal("Require compliant device · Sign In Frequency", failed.ControlsText);

        // Graph leaves the policies out when it won't share Conditional Access data.
        Assert.Null(signIns[1].Policies);
        Assert.False(signIns[1].Device.HasIdentity);
        Assert.Equal("US", signIns[1].Location);
        Assert.Equal(SignInPolicyResult.ReportOnlyFailure, signIns[2].Policies![1].Result);
    }

    [Fact]
    public async Task AsksForTheLastWeekWithReportOnlyResults()
    {
        var stub = new StubGraphClient(new() { ["auditLogs/signIns"] = "signIns" });
        await new SignInLookup(stub, new FixedTime(Now)).RecentAsync("u-o'brien");
        var request = Assert.Single(stub.Sent);
        var query = Uri.UnescapeDataString(request.Url.Query);
        Assert.Contains("$filter=userId eq 'u-o''brien' and createdDateTime ge 2026-10-07T14:00:00Z", query, StringComparison.Ordinal);
        Assert.Contains("$top=50", query, StringComparison.Ordinal);
        Assert.Equal("include-unknown-enum-members", request.Headers["Prefer"]);
    }

    [Fact]
    public void ANonCompliantDeviceIsExplainedWithWhatToCheck()
    {
        var device = new SignInDevice { DeviceId = "dev-1", DisplayName = "CTS-LT-01188", IsCompliant = false, IsManaged = true };
        var diagnosis = SignInDecoder.Diagnose(With(53000, ConditionalAccessStatus.Failure,
            [Policy("Require compliant device", SignInPolicyResult.Failure, "RequireCompliantDevice"), Policy("MFA", SignInPolicyResult.Success, "Mfa")], device));
        Assert.Equal(SignInVerdict.Blocked, diagnosis.Verdict);
        Assert.Equal("Blocked by Conditional Access", diagnosis.Title);
        Assert.Contains("“Require compliant device” refused it (Require compliant device)", diagnosis.Explanation, StringComparison.Ordinal);
        Assert.Contains("Entra sees CTS-LT-01188 as not compliant.", diagnosis.Explanation, StringComparison.Ordinal);
        Assert.Contains("Open CTS-LT-01188 in FieldKit and look at its compliance policies.", diagnosis.WhatToCheck);
        Assert.EndsWith("Request ID req-1 finds this sign-in in Entra's sign-in logs.", diagnosis.WhatToCheck[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void NoDeviceIdentityPointsAtTheBrowser()
    {
        var diagnosis = SignInDecoder.Diagnose(With(53000, ConditionalAccessStatus.Failure,
            [Policy("Compliant only", SignInPolicyResult.Failure, "Require device to be marked as compliant")]));
        Assert.Contains("no device identity", diagnosis.Explanation, StringComparison.Ordinal);
        Assert.Contains(diagnosis.WhatToCheck, check => check.Contains("Microsoft Single Sign On extension", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyClientsBlockedOutrightSaySo()
    {
        var diagnosis = SignInDecoder.Diagnose(With(53003, ConditionalAccessStatus.Failure,
            [Policy("Block legacy authentication", SignInPolicyResult.Failure, "Block")], client: "Exchange ActiveSync"));
        Assert.Contains("Exchange ActiveSync, a legacy client", diagnosis.Explanation, StringComparison.Ordinal);
        Assert.Contains(diagnosis.WhatToCheck, check => check.Contains("Outlook", StringComparison.Ordinal));
    }

    [Fact]
    public void HiddenPoliciesNameWhatsNeededToSeeThem()
    {
        var diagnosis = SignInDecoder.Diagnose(With(53003, ConditionalAccessStatus.Failure, null));
        Assert.Equal(SignInVerdict.Blocked, diagnosis.Verdict);
        Assert.Contains("Policy.Read.ConditionalAccess", diagnosis.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessesMentionReportOnlyPoliciesThatWouldBlock()
    {
        var diagnosis = SignInDecoder.Diagnose(With(0, ConditionalAccessStatus.Success,
            [Policy("MFA", SignInPolicyResult.Success, "Mfa"), Policy("Outside the US", SignInPolicyResult.ReportOnlyFailure, "Block")]));
        Assert.Equal(SignInVerdict.Success, diagnosis.Verdict);
        Assert.Equal("Signed in. 1 Conditional Access policy applied and passed.", diagnosis.Explanation);
        Assert.Equal(["Report-only policy “Outside the US” would block this sign-in once it's turned on."], diagnosis.WhatToCheck);
        Assert.False(diagnosis.IsProblem);
    }

    [Theory]
    [InlineData(50126, SignInVerdict.Failed, "Wrong username or password")]
    [InlineData(50053, SignInVerdict.Failed, "Account locked")]
    [InlineData(50057, SignInVerdict.Failed, "Account disabled")]
    [InlineData(50074, SignInVerdict.Interrupted, "Multifactor authentication needed")]
    [InlineData(50140, SignInVerdict.Interrupted, "Interrupted")]
    [InlineData(50105, SignInVerdict.Failed, "Not assigned to the app")]
    [InlineData(50011, SignInVerdict.Failed, "Redirect URI not registered")]
    [InlineData(7000218, SignInVerdict.Failed, "App registration expects a secret")]
    [InlineData(65001, SignInVerdict.Failed, "Consent missing")]
    [InlineData(50034, SignInVerdict.Failed, "Account not in this directory")]
    public void KnownCodesGetAPlainTitle(int code, SignInVerdict verdict, string title)
    {
        var diagnosis = SignInDecoder.Diagnose(With(code, ConditionalAccessStatus.NotApplied, []));
        Assert.Equal((verdict, title), (diagnosis.Verdict, diagnosis.Title));
    }

    [Fact]
    public void UnknownCodesFallBackToEntrasReason()
    {
        var diagnosis = SignInDecoder.Diagnose(With(90099, ConditionalAccessStatus.NotApplied, []));
        Assert.Equal(SignInVerdict.Failed, diagnosis.Verdict);
        Assert.Equal("Entra's own words.", diagnosis.Explanation);
        Assert.Contains("Look up AADSTS90099 in Microsoft's Entra error code reference.", diagnosis.WhatToCheck);
    }

    /// <summary>Found on a real tenant: AADSTS50011's failureReason came back as an unfilled template.</summary>
    [Fact]
    public void UnfilledTemplatesArentShownAsTheExplanation()
    {
        var signIn = With(90123, ConditionalAccessStatus.NotApplied, []) with { FailureReason = "The {redirectTerm} '{replyAddress}' specified in the request does not match." };
        Assert.Equal("Entra returned error AADSTS90123.", SignInDecoder.Diagnose(signIn).Explanation);
        var redirect = SignInDecoder.Diagnose(signIn with { ErrorCode = 50011, AppName = "FieldKit (IOS)" });
        Assert.Contains("App registrations → FieldKit (IOS) → Authentication", redirect.WhatToCheck[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RequireCompliantDevice", SignInControlKind.CompliantDevice, "Require compliant device")]
    [InlineData("Require device to be marked as compliant", SignInControlKind.CompliantDevice, "Require compliant device")]
    [InlineData("Mfa", SignInControlKind.Mfa, "Require multifactor authentication")]
    [InlineData("Block", SignInControlKind.Block, "Block access")]
    [InlineData("RequireDomainJoinedDevice", SignInControlKind.HybridJoinedDevice, "Require hybrid joined device")]
    [InlineData("RequireApprovedApp", SignInControlKind.ApprovedApp, "Require approved client app")]
    [InlineData("RequireCompliantApp", SignInControlKind.AppProtection, "Require app protection policy")]
    [InlineData("SignInFrequency", SignInControlKind.Other, "Sign In Frequency")]
    public void ControlNamesAreReadLoosely(string raw, SignInControlKind kind, string described)
    {
        Assert.Equal(kind, SignInControls.Kind(raw));
        Assert.Equal(described, SignInControls.Describe(raw));
    }

    [Fact]
    public async Task ThePersonPageListsProblemsFirstAndCanShowEverything()
    {
        var demo = new DemoGraphClient(TimeSpan.Zero);
        var casey = new DirectoryUser { Id = "u-casey", DisplayName = "Casey Nguyen", UserPrincipalName = "casey.nguyen@contoso.com" };
        var person = new PersonViewModel(casey, new DirectoryLookup(demo), () => _expired++, new SignInLookup(demo));
        await person.LoadAsync();
        Assert.Equal(SectionKind.Loaded, person.Devices.Kind);
        Assert.Equal(["Microsoft Teams", "Microsoft Outlook"], person.SignInList.Select(item => item.App));
        Assert.All(person.SignInList, item => Assert.Equal(SignInVerdict.Blocked, item.Verdict));
        Assert.Equal("2 problems in 3 sign-ins over the last 7 days.", person.SignInSummary);
        Assert.Contains("CTS-LT-01188", person.SignInList[0].Subtitle, StringComparison.Ordinal);

        person.OnlyProblems = false;
        Assert.Equal(3, person.SignInList.Count);
        Assert.False(person.ShowsNoProblems);

        var sam = new PersonViewModel(new DirectoryUser { Id = "u-sam", DisplayName = "Sam Lee", UserPrincipalName = "sam.lee@contoso.com" },
            new DirectoryLookup(demo), () => { }, new SignInLookup(demo));
        await sam.LoadAsync();
        Assert.True(sam.ShowsNoProblems);
        Assert.Empty(sam.SignInList);
    }

    [Fact]
    public async Task SignInFailuresStayInTheirSectionAndSayWhy()
    {
        var person = new DirectoryUser { Id = "u-1", DisplayName = "Sam", UserPrincipalName = "sam@contoso.com" };
        var denied = new StubGraphClient(new()
        {
            ["users/u-1/managedDevices"] = "managedDevices_single",
            ["auditLogs/signIns"] = GraphException.Forbidden(),
        });
        var model = new PersonViewModel(person, new DirectoryLookup(denied), () => _expired++, new SignInLookup(denied));
        await model.LoadAsync();
        Assert.Equal(SectionKind.Loaded, model.Devices.Kind);
        Assert.Equal(GraphFeature.SignIns.AccessDeniedMessage(), model.SignIns.Message);
        Assert.Contains("AuditLog.Read.All", model.SignIns.Message, StringComparison.Ordinal);

        var noLicence = new StubGraphClient(new() { ["auditLogs/signIns"] = GraphException.Forbidden(GraphFeatureExtensions.NonPremiumTenant) });
        var unlicensed = new PersonViewModel(person, new DirectoryLookup(noLicence), () => { }, new SignInLookup(noLicence));
        await unlicensed.LoadSignInsAsync();
        Assert.Equal("Sign-in logs need Microsoft Entra ID P1 or P2 in this tenant.", unlicensed.SignIns.Message);
    }

    [Fact]
    public async Task GraphClientKeepsTheCodeOfAForbidden()
    {
        var body = Support.Fixture.Data("error_nonpremium");
        var transport = new ScriptedTransport(Reply.Status(403, body), Reply.Status(403, body));
        var error = await Assert.ThrowsAsync<GraphException>(() => new GraphClient(new FakeTokenProvider(), transport, (_, _) => Task.CompletedTask).SendAsync(GraphRequest.Get("auditLogs/signIns")));
        Assert.Equal((GraphErrorKind.Forbidden, GraphFeatureExtensions.NonPremiumTenant), (error.Kind, error.Code));
    }

    [Fact]
    public async Task TheSignInPageOrdersPoliciesAndHidesTheOnesThatDidNotApply()
    {
        var detail = new SignInDetailViewModel((await Fixture())[0], new FixedTime(Now));
        Assert.Equal("Blocked by Conditional Access", detail.Title);
        Assert.Equal(["Require compliant device for Microsoft 365", "Require MFA for all users"], detail.PolicyList.Select(policy => policy.Name));
        Assert.Equal([InfoTone.Critical, InfoTone.Good], detail.PolicyList.Select(policy => policy.Tone));
        Assert.True(detail.HasPoliciesThatDidNotApply);
        detail.ShowsAllPolicies = true;
        Assert.Equal("Block legacy authentication", detail.PolicyList[^1].Name);

        Assert.True(detail.CanOpenDevice);
        Assert.Equal("Open CTS-LT-01188 in FieldKit", detail.OpenDeviceText);
        Assert.Contains(detail.DeviceRows, row => row is { Label: "Compliant", Value: "No", Tone: InfoTone.Critical });
        Assert.Contains(detail.DetailRows, row => row is { Label: "Error code", Value: "AADSTS53000" });
        Assert.Equal("8f2c5c1e-6a2b-4d7e-9a11-0c5d2e7f3b01", detail.RequestId);

        var hidden = new SignInDetailViewModel((await Fixture())[1], new FixedTime(Now));
        Assert.False(hidden.PoliciesVisible);
        Assert.False(hidden.CanOpenDevice);
    }

    [Fact]
    public void ThePermissionsAreListedForSetup()
    {
        var names = GraphPermission.All.Select(permission => permission.Name).ToList();
        Assert.Contains("AuditLog.Read.All", names);
        Assert.Contains("Policy.Read.ConditionalAccess", names);
        Assert.Contains("Policy.Read.ConditionalAccess", GraphPermission.Granted(["Policy.ReadWrite.ConditionalAccess"]) ?? new HashSet<string>());
    }
}
