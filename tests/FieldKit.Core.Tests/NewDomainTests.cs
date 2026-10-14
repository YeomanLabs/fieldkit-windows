using System.Text;

namespace FieldKit.Core.Tests;

public sealed class NewDomainTests
{
    [Fact]
    public void LapsPasswordDecodesUtf8AndUtf16LittleEndian()
    {
        Assert.Equal("Kp7#vQ2m-Lx9!dRw", LocalAdminCredential.DecodePassword("S3A3I3ZRMm0tTHg5IWRSdw=="));
        var utf16 = Convert.ToBase64String(Encoding.Unicode.GetBytes("Kp7#vQ2m"));
        Assert.Equal("Kp7#vQ2m", LocalAdminCredential.DecodePassword(utf16));
        // From Microsoft's documentation example.
        Assert.Equal("HrK82c", LocalAdminCredential.DecodePassword("SAByAEsAOAAyAGMA"));
        Assert.Null(LocalAdminCredential.DecodePassword("not base64!"));
        Assert.Null(LocalAdminCredential.DecodePassword(""));
    }

    [Theory]
    [InlineData("1", "OS drive")]
    [InlineData("operatingSystemVolume", "OS drive")]
    [InlineData("2", "Fixed data drive")]
    [InlineData("FixedDataVolume", "Fixed data drive")]
    [InlineData("3", "Removable drive")]
    [InlineData(null, "Drive")]
    [InlineData("99", "Drive")]
    public void BitLockerVolumeNames(string? raw, string expected)
    {
        Assert.Equal(expected, BitLockerRecoveryKey.VolumeName(raw));
    }

    [Fact]
    public void ComplianceSettingNames()
    {
        Assert.Equal("Require password", CompliancePolicySetting.DisplayName("Require password", "X.passwordRequired"));
        Assert.Equal("bitLockerEnabled", CompliancePolicySetting.DisplayName("", "Windows10CompliancePolicy.bitLockerEnabled"));
        Assert.Equal("Unnamed setting", CompliancePolicySetting.DisplayName(null, null));
    }

    [Fact]
    public void CompliancePoliciesSortProblemsFirstAndHideIrrelevant()
    {
        static CompliancePolicyState Policy(string name, PolicyComplianceState state) => new(name, name, state, []);
        var sorted = CompliancePolicyState.SortedForDisplay(
        [
            Policy("b compliant", PolicyComplianceState.Compliant),
            Policy("z broken", PolicyComplianceState.Noncompliant),
            Policy("macOS", PolicyComplianceState.NotApplicable),
            Policy("a compliant", PolicyComplianceState.Compliant),
            Policy("unassigned", PolicyComplianceState.NotAssigned),
            Policy("c error", PolicyComplianceState.Error),
        ]);
        Assert.Equal(["c error", "z broken", "a compliant", "b compliant"], sorted.Select(policy => policy.DisplayName));
    }

    [Fact]
    public void PolicyStatesParseGraphCasing()
    {
        Assert.Equal(PolicyComplianceState.Noncompliant, PolicyComplianceState.FromRaw("nonCompliant"));
        Assert.False(PolicyComplianceState.FromRaw("notApplicable").IsRelevant);
        Assert.True(PolicyComplianceState.FromRaw("conflict").IsProblem);
        Assert.Equal(PolicyComplianceState.Unknown, PolicyComplianceState.FromRaw(null));
    }

    [Fact]
    public void GroupTags()
    {
        Assert.Equal("CTS-Standard", GroupTag.Normalized("  CTS-Standard "));
        Assert.Equal("", GroupTag.Normalized(""));
        Assert.Equal("Kiosk/Floor 2", GroupTag.Normalized("Kiosk/Floor 2"));
        Assert.Null(GroupTag.Normalized("two\nlines"));
        Assert.Null(GroupTag.Normalized(new string('a', 101)));
    }

    [Fact]
    public void SearchTermsAreSafeForSearchClauses()
    {
        Assert.Equal("alex", ODataLiteral.SearchTerm(" alex "));
        Assert.Equal("alex", ODataLiteral.SearchTerm("al\"ex\\"));
        Assert.Equal("alex doe", ODataLiteral.SearchTerm("alex   doe"));
        Assert.Null(ODataLiteral.SearchTerm("a"));
        Assert.Null(ODataLiteral.SearchTerm("\"\""));
    }

    [Fact]
    public void ActionGating()
    {
        Assert.False(DeviceAction.Sync.RequiresOwnerVerification());
        Assert.True(DeviceAction.Restart.RequiresOwnerVerification());
        Assert.True(DeviceAction.Retire.RequiresOwnerVerification());
        Assert.True(DeviceAction.Retire.RequiresTypedConfirmation());
        Assert.False(DeviceAction.Restart.RequiresTypedConfirmation());
    }
}
