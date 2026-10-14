namespace FieldKit.Core.Tests;

public sealed class FlagEvaluationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_791_000_000);
    private static readonly AuditSettings Settings = new() { StaleThresholdDays = 30 };

    private static readonly AutopilotDevice AutopilotRecord = new()
    {
        Id = "a1",
        SerialNumber = "5CG1234XYZ",
        EnrollmentState = AutopilotEnrollmentState.Enrolled,
    };

    private static readonly Lookup<IReadOnlyList<IntuneDevice>> IntuneNotFound = Lookup<IReadOnlyList<IntuneDevice>>.NotFound;
    private static readonly Lookup<IReadOnlyList<AutopilotDevice>> AutopilotNotFound = Lookup<IReadOnlyList<AutopilotDevice>>.NotFound;
    private static readonly Lookup<EntraDevice> EntraSkipped = Lookup<EntraDevice>.Skipped;

    private static IntuneDevice Intune(
        string id = "i1",
        string name = "CTS-LT-01234",
        string upn = "alex@contoso.com",
        double? daysSinceSync = 1,
        ComplianceState? compliance = null,
        string? entraId = "c0a8e2f4-1b3d-4e5f-8a9b-0c1d2e3f4a5b") =>
        new()
        {
            Id = id,
            DeviceName = name,
            SerialNumber = "5CG1234XYZ",
            UserPrincipalName = upn,
            UserDisplayName = "Alex",
            LastSyncDateTime = daysSinceSync is { } days ? Now - TimeSpan.FromDays(days) : null,
            ComplianceState = compliance ?? ComplianceState.Compliant,
            OperatingSystem = "Windows",
            OsVersion = "10.0",
            Model = "EliteBook",
            Manufacturer = "HP",
            OwnerType = "company",
            AzureADDeviceId = entraId,
            IsEncrypted = true,
        };

    private static EntraDevice Entra(string name = "CTS-LT-01234", bool enabled = true) =>
        new() { Id = "e1", DisplayName = name, AccountEnabled = enabled, TrustType = EntraTrustType.HybridJoined, OperatingSystem = "Windows" };

    private static Lookup<IReadOnlyList<IntuneDevice>> IntuneFound(params IntuneDevice[] devices) =>
        Lookup<IReadOnlyList<IntuneDevice>>.Found(devices);

    private static Lookup<IReadOnlyList<AutopilotDevice>> AutopilotFound(params AutopilotDevice[] records) =>
        Lookup<IReadOnlyList<AutopilotDevice>>.Found(records);

    private static Lookup<EntraDevice> EntraFound(EntraDevice device) => Lookup<EntraDevice>.Found(device);

    private static IReadOnlyList<Flag> Evaluate(
        Lookup<IReadOnlyList<IntuneDevice>> intune,
        Lookup<EntraDevice> entra,
        Lookup<IReadOnlyList<AutopilotDevice>> autopilot,
        AuditSettings? settings = null,
        string? selectedIntuneId = null) =>
        FlagEvaluator.Evaluate(intune, entra, autopilot, settings ?? Settings, selectedIntuneId, Now);

    private static HashSet<FlagKind> Kinds(IReadOnlyList<Flag> flags) => flags.Select(flag => flag.Kind).ToHashSet();

    [Fact]
    public void HealthyDeviceHasNoFlags()
    {
        Assert.Empty(Evaluate(IntuneFound(Intune()), EntraFound(Entra()), AutopilotFound(AutopilotRecord)));
    }

    [Fact]
    public void AutopilotOnlyIsNotInIntune()
    {
        var flags = Evaluate(IntuneNotFound, EntraSkipped, AutopilotFound(AutopilotRecord));
        Assert.Equal([FlagKind.NotInIntune], flags.Select(flag => flag.Kind));
        Assert.Equal(FlagSeverity.Info, flags[0].Severity);
    }

    [Fact]
    public void MissingEverywhereIsUnknown()
    {
        var flags = Evaluate(IntuneNotFound, EntraSkipped, AutopilotNotFound);
        Assert.Equal([FlagKind.UnknownDevice], flags.Select(flag => flag.Kind));
        Assert.Equal(FlagSeverity.Warning, flags[0].Severity);
    }

    [Fact]
    public void EmptyFoundListsCountAsNotFound()
    {
        var flags = Evaluate(IntuneFound(), EntraSkipped, AutopilotFound());
        Assert.Equal([FlagKind.UnknownDevice], flags.Select(flag => flag.Kind));
    }

    [Fact]
    public void UnavailableLookupsNeverClaimMissing()
    {
        Assert.Empty(Evaluate(IntuneNotFound, EntraSkipped, Lookup<IReadOnlyList<AutopilotDevice>>.Unavailable));
        Assert.Empty(Evaluate(Lookup<IReadOnlyList<IntuneDevice>>.Unavailable, Lookup<EntraDevice>.Unavailable, AutopilotNotFound));
    }

    [Fact]
    public void StaleUsesThreshold()
    {
        var fresh = Evaluate(IntuneFound(Intune(daysSinceSync: 29)), EntraFound(Entra()), AutopilotNotFound);
        Assert.DoesNotContain(FlagKind.Stale, Kinds(fresh));

        var old = Evaluate(IntuneFound(Intune(daysSinceSync: 31)), EntraFound(Entra()), AutopilotNotFound);
        Assert.Contains(FlagKind.Stale, Kinds(old));

        var custom = new AuditSettings { StaleThresholdDays = 7 };
        var week = Evaluate(IntuneFound(Intune(daysSinceSync: 8)), EntraFound(Entra()), AutopilotNotFound, custom);
        Assert.Contains(FlagKind.Stale, Kinds(week));
    }

    [Fact]
    public void NeverSyncedIsStale()
    {
        var flags = Evaluate(IntuneFound(Intune(daysSinceSync: null)), EntraFound(Entra()), AutopilotNotFound);
        Assert.Contains(FlagKind.Stale, Kinds(flags));
    }

    [Fact]
    public void NoPrimaryUser()
    {
        var flags = Evaluate(IntuneFound(Intune(upn: " ")), EntraFound(Entra()), AutopilotNotFound);
        Assert.Equal(FlagSeverity.Info, flags.Single(flag => flag.Kind == FlagKind.NoPrimaryUser).Severity);
    }

    [Theory]
    [InlineData("noncompliant", FlagSeverity.Warning)]
    [InlineData("unknown", FlagSeverity.Warning)]
    [InlineData("error", FlagSeverity.Warning)]
    [InlineData("conflict", FlagSeverity.Warning)]
    [InlineData("configManager", FlagSeverity.Warning)]
    [InlineData("inGracePeriod", FlagSeverity.Info)]
    public void NoncompliantSeverity(string state, FlagSeverity severity)
    {
        var flags = Evaluate(IntuneFound(Intune(compliance: ComplianceState.FromRaw(state))), EntraFound(Entra()), AutopilotNotFound);
        Assert.Equal(severity, flags.Single(flag => flag.Kind == FlagKind.Noncompliant).Severity);
    }

    [Fact]
    public void NamingConvention()
    {
        var convention = new AuditSettings { StaleThresholdDays = 30, NamingConventionRegex = "CTS-(LT|DT)-[0-9]{5}" };
        var good = Evaluate(IntuneFound(Intune(name: "cts-lt-01234")), EntraFound(Entra(name: "cts-lt-01234")), AutopilotNotFound, convention);
        Assert.DoesNotContain(FlagKind.NameConvention, Kinds(good));

        var bad = Evaluate(IntuneFound(Intune(name: "DESKTOP-7Q2LMNP")), EntraFound(Entra(name: "DESKTOP-7Q2LMNP")), AutopilotNotFound, convention);
        Assert.Contains(FlagKind.NameConvention, Kinds(bad));

        // Whole-name match: a valid name with a suffix fails.
        var suffix = Evaluate(IntuneFound(Intune(name: "CTS-LT-01234-OLD")), EntraFound(Entra(name: "CTS-LT-01234-OLD")), AutopilotNotFound, convention);
        Assert.Contains(FlagKind.NameConvention, Kinds(suffix));
    }

    [Fact]
    public void EmptyOrInvalidRegexDisablesNamingCheck()
    {
        foreach (var pattern in new[] { "", "([unclosed" })
        {
            var settings = new AuditSettings { StaleThresholdDays = 30, NamingConventionRegex = pattern };
            var flags = Evaluate(IntuneFound(Intune(name: "anything")), EntraFound(Entra(name: "anything")), AutopilotNotFound, settings);
            Assert.DoesNotContain(FlagKind.NameConvention, Kinds(flags));
        }
        Assert.False(AuditSettings.IsValidNamingRegex("([unclosed"));
        Assert.True(AuditSettings.IsValidNamingRegex(""));
    }

    [Fact]
    public void NameMismatchIgnoresCase()
    {
        var same = Evaluate(IntuneFound(Intune(name: "CTS-LT-01234")), EntraFound(Entra(name: "cts-lt-01234")), AutopilotNotFound);
        Assert.DoesNotContain(FlagKind.NameMismatch, Kinds(same));

        var different = Evaluate(IntuneFound(Intune(name: "CTS-LT-01234")), EntraFound(Entra(name: "CTS-LT-09999")), AutopilotNotFound);
        Assert.Contains(FlagKind.NameMismatch, Kinds(different));
    }

    [Fact]
    public void EntraDisabledIsCritical()
    {
        var flags = Evaluate(IntuneFound(Intune()), EntraFound(Entra(enabled: false)), AutopilotNotFound);
        Assert.Equal(FlagKind.EntraDisabled, flags[0].Kind);
        Assert.Equal(FlagSeverity.Critical, flags[0].Severity);
    }

    [Fact]
    public void NoEntraObjectOnlyWhenIntuneHasAnId()
    {
        var missing = Evaluate(IntuneFound(Intune()), Lookup<EntraDevice>.NotFound, AutopilotNotFound);
        Assert.Equal(FlagKind.NoEntraObject, missing[0].Kind);
        Assert.Equal(FlagSeverity.Critical, missing[0].Severity);

        foreach (var id in new[] { null, "", "00000000-0000-0000-0000-000000000000" })
        {
            var flags = Evaluate(IntuneFound(Intune(entraId: id)), Lookup<EntraDevice>.NotFound, AutopilotNotFound);
            Assert.DoesNotContain(FlagKind.NoEntraObject, Kinds(flags));
        }

        var failed = Evaluate(IntuneFound(Intune()), Lookup<EntraDevice>.Unavailable, AutopilotNotFound);
        Assert.DoesNotContain(FlagKind.NoEntraObject, Kinds(failed));
    }

    [Fact]
    public void DuplicatesEvaluateSelectedRecord()
    {
        var first = Intune(id: "i1", upn: "");
        var second = Intune(id: "i2", upn: "sam@contoso.com");
        var defaultFlags = Evaluate(IntuneFound(first, second), EntraFound(Entra()), AutopilotNotFound);
        Assert.Equal(new HashSet<FlagKind> { FlagKind.DuplicateIntuneRecords, FlagKind.NoPrimaryUser }, Kinds(defaultFlags));

        var selected = Evaluate(IntuneFound(first, second), EntraFound(Entra()), AutopilotNotFound, selectedIntuneId: "i2");
        Assert.Equal(new HashSet<FlagKind> { FlagKind.DuplicateIntuneRecords }, Kinds(selected));
    }

    [Fact]
    public void SortedBySeverityThenRuleOrder()
    {
        var flags = Evaluate(
            IntuneFound(Intune(name: "X-1", upn: "", daysSinceSync: 90, compliance: ComplianceState.Noncompliant)),
            EntraFound(Entra(name: "Y-2", enabled: false)),
            AutopilotNotFound);
        Assert.Equal(
            [FlagKind.EntraDisabled, FlagKind.Stale, FlagKind.Noncompliant, FlagKind.NameMismatch, FlagKind.NoPrimaryUser],
            flags.Select(flag => flag.Kind));
    }
}
