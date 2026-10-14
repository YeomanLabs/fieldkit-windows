namespace FieldKit.Core.Tests;

public sealed class SiteWalkCsvTests
{
    private static SiteWalkCsvRow Row(string? name = "CTS-LT-01234", string? user = "alex@contoso.com", IReadOnlyList<string>? flags = null) =>
        new()
        {
            Site = "Contoso HQ",
            Area = "Floor 2",
            ScannedAt = DateTimeOffset.FromUnixTimeSeconds(1_791_555_751),
            Serial = "5CG1234XYZ",
            IntuneDeviceName = name,
            EntraDisplayName = name,
            PrimaryUser = user,
            LastSync = null,
            Compliance = "Compliant",
            TrustType = "Hybrid joined",
            AutopilotGroupTag = null,
            Flags = flags ?? [],
        };

    [Fact]
    public void HeaderMatchesSpec()
    {
        var csv = SiteWalkCsv.Make([], TimeZoneInfo.Utc);
        Assert.Equal("﻿site,area,scannedAt,serial,intuneDeviceName,entraDisplayName,primaryUser,lastSync,compliance,trustType,autopilotGroupTag,flags\r\n", csv);
    }

    [Fact]
    public void RowFormatting()
    {
        var csv = SiteWalkCsv.Make([Row(flags: ["stale", "noncompliant"])], TimeZoneInfo.Utc);
        var lines = csv.Split("\r\n");
        Assert.Equal("Contoso HQ,Floor 2,2026-10-09 14:22:31,5CG1234XYZ,CTS-LT-01234,CTS-LT-01234,alex@contoso.com,,Compliant,Hybrid joined,,stale; noncompliant", lines[1]);
    }

    [Fact]
    public void QuotesSpecialCharacters()
    {
        Assert.Equal("plain", SiteWalkCsv.Escape("plain"));
        Assert.Equal("\"a,b\"", SiteWalkCsv.Escape("a,b"));
        Assert.Equal("\"say \"\"hi\"\"\"", SiteWalkCsv.Escape("say \"hi\""));
        Assert.Equal("\"two\nlines\"", SiteWalkCsv.Escape("two\nlines"));
        Assert.Equal("\"crlf\r\nline\"", SiteWalkCsv.Escape("crlf\r\nline"));
        Assert.Equal("\" padded\"", SiteWalkCsv.Escape(" padded"));
    }

    [Fact]
    public void NeutralisesFormulas()
    {
        Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\"", SiteWalkCsv.Escape("=HYPERLINK(\"x\")"));
        Assert.Equal("'+1", SiteWalkCsv.Escape("+1"));
        Assert.Equal("'-1", SiteWalkCsv.Escape("-1"));
        Assert.Equal("'@SUM(A1)", SiteWalkCsv.Escape("@SUM(A1)"));
    }
}
