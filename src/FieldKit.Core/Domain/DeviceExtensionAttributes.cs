namespace FieldKit.Core.Domain;

/// <summary>
/// An Entra device's <c>extensionAttribute1</c>–<c>extensionAttribute15</c>: free text an admin
/// sets for dynamic groups (<c>device.extensionAttribute3 -eq "Kiosk"</c>) and assignment
/// filters. Empty means not set. Windows only (added 2026-10-13; iOS doesn't show them).
/// </summary>
public sealed class DeviceExtensionAttributes : IEquatable<DeviceExtensionAttributes>
{
    public const int Count = 15;

    /// <summary>Longest value kept, as for on-premises extension attributes.</summary>
    public const int MaxLength = 1024;

    private readonly string[] _values;

    /// <param name="values">Attributes 1 to 15 in order; missing or null ones are not set.</param>
    public DeviceExtensionAttributes(IEnumerable<string?> values)
    {
        _values = new string[Count];
        var index = 0;
        foreach (var value in values.Take(Count)) _values[index++] = value?.Trim() ?? "";
        for (; index < Count; index++) _values[index] = "";
    }

    public static DeviceExtensionAttributes None { get; } = new([]);

    /// <summary>The value of attribute <paramref name="number"/> (1–15); empty when not set.</summary>
    public string this[int number] => _values[Index(number)];

    /// <summary>The attributes that have a value, in order.</summary>
    public IReadOnlyList<ExtensionAttribute> Set =>
        _values.Select((value, index) => new ExtensionAttribute(index + 1, value)).Where(attribute => attribute.Value.Length > 0).ToList();

    public bool IsEmpty => _values.All(value => value.Length == 0);

    /// <summary>Graph's property name: <c>extensionAttribute3</c>.</summary>
    public static string Name(int number) => $"extensionAttribute{Index(number) + 1}";

    /// <summary>These attributes with <paramref name="changes"/> applied; a null or empty value clears one.</summary>
    public DeviceExtensionAttributes With(IReadOnlyDictionary<int, string?> changes)
    {
        var values = (string?[])_values.Clone();
        foreach (var (number, value) in changes) values[Index(number)] = value;
        return new DeviceExtensionAttributes(values);
    }

    public bool Equals(DeviceExtensionAttributes? other) => other is not null && _values.SequenceEqual(other._values, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as DeviceExtensionAttributes);

    public override int GetHashCode() => _values.Aggregate(0, (hash, value) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(value)));

    public override string ToString() => IsEmpty ? "(none)" : string.Join(", ", Set.Select(attribute => $"{Name(attribute.Number)}={attribute.Value}"));

    private static int Index(int number) =>
        number is >= 1 and <= Count ? number - 1 : throw new ArgumentOutOfRangeException(nameof(number), number, "Extension attributes are numbered 1 to 15.");
}
