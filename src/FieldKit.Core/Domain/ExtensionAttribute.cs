namespace FieldKit.Core.Domain;

/// <summary>One extension attribute that has a value, as the device page lists it.</summary>
public sealed record ExtensionAttribute(int Number, string Value)
{
    /// <summary><c>extensionAttribute3</c>: the name dynamic group rules and filters use.</summary>
    public string Name => DeviceExtensionAttributes.Name(Number);
}
