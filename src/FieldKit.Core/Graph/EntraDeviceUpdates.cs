using System.Text.Encodings.Web;
using System.Text.Json;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// Writes to an Entra device object. Only its extension attributes (approved by the owner on
/// 2026-10-13; Windows only). Graph accepts this from a signed-in user only with the delegated
/// <c>Directory.AccessAsUser.All</c> permission and the Intune Administrator role.
/// </summary>
public sealed class EntraDeviceUpdates(IGraphClient client)
{
    /// <summary>Sets or clears the given attributes in one PATCH, leaving the others as they are.</summary>
    /// <param name="objectId">The Entra object ID (<c>devices/{id}</c>), not the device ID.</param>
    /// <param name="changes">Attribute number (1–15) to its new value; null or empty clears it.</param>
    public Task SetExtensionAttributesAsync(string objectId, IReadOnlyDictionary<int, string?> changes, CancellationToken cancellationToken = default) =>
        changes.Count == 0
            ? Task.CompletedTask
            : client.SendAsync(GraphRequest.Patch($"devices/{objectId}", Body(changes)), cancellationToken);

    /// <summary><c>{"extensionAttributes":{"extensionAttribute3":"Kiosk","extensionAttribute7":null}}</c>, in number order.</summary>
    internal static byte[] Body(IReadOnlyDictionary<int, string?> changes)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("extensionAttributes");
            foreach (var (number, value) in changes.OrderBy(change => change.Key))
            {
                var name = DeviceExtensionAttributes.Name(number);
                if (value?.Trim() is { Length: > 0 } text) writer.WriteString(name, text);
                else writer.WriteNull(name);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}
