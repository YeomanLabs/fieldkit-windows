using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldKit.Core.Graph;

/// <summary>
/// Graph dates via <see cref="GraphJson.ParseDate"/>. Dates are informational, so one that can't
/// be read becomes <see cref="DateTimeOffset.MinValue"/> (shown as "never" via
/// <see cref="GraphJson.Meaningful"/>) instead of failing the whole response.
/// </summary>
internal sealed class GraphDateConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("The JSON value could not be converted to a date.");
        }
        return GraphJson.ParseDate(reader.GetString() ?? "") ?? DateTimeOffset.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
}
