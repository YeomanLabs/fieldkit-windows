using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>A device's <c>extensionAttributes</c> object (<c>onPremisesExtensionAttributes</c>): 15 nullable strings.</summary>
internal sealed class ExtensionAttributesDto
{
    public string? ExtensionAttribute1 { get; init; }

    public string? ExtensionAttribute2 { get; init; }

    public string? ExtensionAttribute3 { get; init; }

    public string? ExtensionAttribute4 { get; init; }

    public string? ExtensionAttribute5 { get; init; }

    public string? ExtensionAttribute6 { get; init; }

    public string? ExtensionAttribute7 { get; init; }

    public string? ExtensionAttribute8 { get; init; }

    public string? ExtensionAttribute9 { get; init; }

    public string? ExtensionAttribute10 { get; init; }

    public string? ExtensionAttribute11 { get; init; }

    public string? ExtensionAttribute12 { get; init; }

    public string? ExtensionAttribute13 { get; init; }

    public string? ExtensionAttribute14 { get; init; }

    public string? ExtensionAttribute15 { get; init; }

    public DeviceExtensionAttributes ToDomain() =>
        new(
        [
            ExtensionAttribute1, ExtensionAttribute2, ExtensionAttribute3, ExtensionAttribute4, ExtensionAttribute5,
            ExtensionAttribute6, ExtensionAttribute7, ExtensionAttribute8, ExtensionAttribute9, ExtensionAttribute10,
            ExtensionAttribute11, ExtensionAttribute12, ExtensionAttribute13, ExtensionAttribute14, ExtensionAttribute15,
        ]);
}
