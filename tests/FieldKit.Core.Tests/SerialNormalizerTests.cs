namespace FieldKit.Core.Tests;

public sealed class SerialNormalizerTests
{
    [Theory]
    [InlineData("5CG1234XYZ", "5CG1234XYZ")]
    [InlineData("  5cg1234xyz \n", "5CG1234XYZ")]
    [InlineData("*5CG1234XYZ*", "5CG1234XYZ")]
    [InlineData("S/N 5CG1234XYZ", "5CG1234XYZ")]
    [InlineData("S/N:5CG1234XYZ", "5CG1234XYZ")]
    [InlineData("s/n5cg1234xyz", "5CG1234XYZ")]
    [InlineData("SN:PF2ABCDE", "PF2ABCDE")]
    [InlineData("SN: PF2ABCDE", "PF2ABCDE")]
    [InlineData("SN PF2ABCDE", "PF2ABCDE")]
    [InlineData("SN#PF2ABCDE", "PF2ABCDE")]
    [InlineData("Serial: 0F3456789012", "0F3456789012")]
    [InlineData("SERIAL NO. C02XK1ABJG5H", "C02XK1ABJG5H")]
    [InlineData("Serial Number: 7HQ2LM3", "7HQ2LM3")]
    [InlineData("(S) 5CG1234XYZ", "5CG1234XYZ")]
    [InlineData("(S)5CG1234XYZ", "5CG1234XYZ")]
    [InlineData("*SN:PF2ABCDE*", "PF2ABCDE")]
    [InlineData("ABC-12345", "ABC-12345")]
    public void NormalizesLabelFormats(string raw, string expected)
    {
        Assert.Equal(expected, SerialNormalizer.Normalize(raw));
    }

    [Fact]
    public void KeepsSerialsThatStartWithSN()
    {
        // Letter-only prefixes are stripped only when a separator follows.
        Assert.Equal("SNAP12345", SerialNormalizer.Normalize("SNAP12345"));
        Assert.Equal("SERIALIZED1", SerialNormalizer.Normalize("SERIALIZED1"));
    }

    [Theory]
    [InlineData("https://support.hp.com/check?sn=5CG1234XYZ&lang=en", "5CG1234XYZ")]
    [InlineData("https://example.com/warranty?Serial=pf2abcde", "PF2ABCDE")]
    [InlineData("http://x.example/?serialNumber=7HQ2LM3", "7HQ2LM3")]
    [InlineData("https://x.example/?SerialNo=7HQ2LM3", "7HQ2LM3")]
    public void ExtractsSerialFromUrl(string raw, string expected)
    {
        Assert.Equal(expected, SerialNormalizer.Normalize(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]                                // too short
    [InlineData("SN:1234")]                             // too short after stripping
    [InlineData("1234567890123456789012345678901")]     // 31 characters
    [InlineData("https://example.com/product?id=42")]   // URL without a serial key
    [InlineData("5CG 1234 XYZ")]                        // internal spaces
    [InlineData("PO#8891/22")]                          // punctuation
    [InlineData("Ünïcode123")]                          // non-ASCII
    public void RejectsNonSerials(string raw)
    {
        Assert.Null(SerialNormalizer.Normalize(raw));
    }

    [Fact]
    public void AcceptsLengthBoundaries()
    {
        Assert.Equal("ABCDE", SerialNormalizer.Normalize("ABCDE"));
        Assert.NotNull(SerialNormalizer.Normalize(new string('A', 30)));
    }
}
