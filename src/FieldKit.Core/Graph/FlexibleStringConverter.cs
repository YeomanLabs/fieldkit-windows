using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldKit.Core.Graph;

/// <summary>A field Graph sends as a string or a number (BitLocker's <c>volumeType</c>). Anything else reads as null.</summary>
internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var number) ? number.ToString(CultureInfo.InvariantCulture) : null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
