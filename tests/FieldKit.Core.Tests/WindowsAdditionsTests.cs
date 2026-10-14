using System.Globalization;
using System.Text;

namespace FieldKit.Core.Tests;

/// <summary>New for Windows: behaviour the C# port adds or has to pin down explicitly.</summary>
public sealed class WindowsAdditionsTests
{
    [Fact]
    public void FlagKindRawValuesMatchIos()
    {
        // Saved scan records and CSV exports use these names on both platforms.
        Assert.Equal("notInIntune", FlagKind.NotInIntune.RawValue());
        Assert.Equal("duplicateIntuneRecords", FlagKind.DuplicateIntuneRecords.RawValue());
        foreach (var kind in Enum.GetValues<FlagKind>())
        {
            Assert.Equal(kind, FlagKindExtensions.FromRawValue(kind.RawValue()));
        }
        Assert.Null(FlagKindExtensions.FromRawValue("NotInIntune"));
        Assert.Equal(["info", "warning", "critical"], Enum.GetValues<FlagSeverity>().Select(severity => severity.RawValue()));
        Assert.Equal(FlagSeverity.Critical, FlagSeverityExtensions.FromRawValue("critical"));
    }

    [Fact]
    public void StorageSizesReadLikeFileExplorer()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.Equal("512 bytes", StorageUsage.Format(512));
            Assert.Equal("112 GB", StorageUsage.Format(120_259_084_288));
            Assert.Equal("237 GB", StorageUsage.Format(255_013_683_200));
            Assert.Equal("9.00 GB", StorageUsage.Format(9_663_676_416));
            Assert.Equal("1.50 KB", StorageUsage.Format(1_536));
            Assert.Equal("112 GB free of 237 GB", StorageUsage.Create(255_013_683_200, 120_259_084_288)?.Summary);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void SecretsStayOutOfToString()
    {
        var key = new BitLockerRecoveryKey("k-1", "118657-202301-591943-336754-218812-490765-611820-264407", null, "OS drive");
        Assert.DoesNotContain("118657", key.ToString(), StringComparison.Ordinal);
        var credential = new LocalAdminCredential("PC1", "LocalAdmin", "Kp7#vQ2m-Lx9!dRw", null, null);
        Assert.DoesNotContain("Kp7#", credential.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NamingRegexMatchesTheWholeNameEvenWithAlternation()
    {
        var regex = Assert.IsType<System.Text.RegularExpressions.Regex>(AuditSettings.CompileNamingRegex("CTS|DESK"));
        Assert.Matches(regex, "desk");
        Assert.DoesNotMatch(regex, "CTSX");
        Assert.DoesNotMatch(regex, "XDESK");
        // Wrapping the pattern in a group mustn't make an unbalanced one valid.
        Assert.False(AuditSettings.IsValidNamingRegex("a)(b"));
    }

    [Fact]
    public void GroupTagsRejectInvisibleFormatCharacters()
    {
        Assert.Null(GroupTag.Normalized("CTS​Kiosk"));
        Assert.Equal("Kiosk-Été", GroupTag.Normalized("Kiosk-Été"));
    }

    [Theory]
    [InlineData("""{"value":[{"id":5}]}""", "unexpected type at value.0.id")]
    [InlineData("""{"value":[{"id":null}]}""", "empty value.0.id")]
    [InlineData("""{"value":[{"id":"i-1","lastSyncDateTime":5}]}""", "unexpected type at value.0.lastSyncDateTime")]
    [InlineData("""{"items":[]}""", "missing value")]
    public void DecodingErrorsDescribeTheShapeNotTheValue(string json, string expected)
    {
        var error = Assert.Throws<GraphException>(() => GraphJson.Decode<GraphCollection<ManagedDeviceDto>>(Encoding.UTF8.GetBytes(json)));
        Assert.Equal(expected, error.Detail);
    }

    [Fact]
    public void TruncatedBodiesReportWhereReadingStoppedWithoutValues()
    {
        var json = """{"value":[{"id":"SECRET-1","deviceName":"PC1"}""";
        var error = Assert.Throws<GraphException>(() => GraphJson.Decode<GraphCollection<ManagedDeviceDto>>(Encoding.UTF8.GetBytes(json)));
        Assert.StartsWith("unreadable value.", error.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PC1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultLookupsAreUnavailable()
    {
        // A lookup nobody has filled in yet must never be read as "not found".
        Assert.Equal(LookupState.Unavailable, default(Lookup<EntraDevice>).State);
    }
}
