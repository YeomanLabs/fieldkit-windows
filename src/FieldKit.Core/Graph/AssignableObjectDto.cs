using System.Text.Json.Serialization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// Any assignable Intune object listed with <c>$expand=assignments</c>. Apps and most policies
/// use <c>displayName</c>; Settings Catalog uses <c>name</c>.
/// </summary>
internal sealed class AssignableObjectDto
{
    public required string Id { get; init; }

    [JsonPropertyName("@odata.type")]
    public string? Type { get; init; }

    public string? DisplayName { get; init; }

    public string? Name { get; init; }

    public string? Publisher { get; init; }

    public IReadOnlyList<AssignmentDto>? Assignments { get; init; }

    public AssignedItem ToDomain(AssignmentKind kind) =>
        new()
        {
            Id = Id,
            Kind = kind,
            Name = DisplayName ?? Name ?? "Unnamed",
            Detail = Detail(kind),
            Assignments = (Assignments ?? []).Select(assignment => assignment.ToDomain()).ToList(),
            TypeName = Type?.Replace("#microsoft.graph.", "", StringComparison.Ordinal) ?? "",
        };

    private string Detail(AssignmentKind kind)
    {
        if (kind is AssignmentKind.Remediation or AssignmentKind.Script)
        {
            return string.IsNullOrEmpty(Publisher) ? "" : Publisher;
        }
        if (Type is null) return "";
        var raw = Type.Replace("#microsoft.graph.", "", StringComparison.Ordinal);
        return raw switch
        {
            "win32LobApp" => "Windows app (Win32)",
            "winGetApp" => "WinGet app",
            "windowsMicrosoftEdgeApp" => "Microsoft Edge",
            "officeSuiteApp" => "Microsoft 365 Apps",
            "windowsMobileMSI" => "Windows MSI",
            "windowsUniversalAppX" or "windowsAppX" => "Windows AppX",
            "microsoftStoreForBusinessApp" or "windowsStoreApp" => "Microsoft Store",
            "iosStoreApp" or "iosVppApp" => "iOS store app",
            "iosLobApp" => "iOS line-of-business",
            "managedIOSStoreApp" or "managedAndroidStoreApp" => "Managed store app",
            "androidManagedStoreApp" or "androidStoreApp" => "Android store app",
            "macOSLobApp" or "macOSDmgApp" or "macOSPkgApp" => "macOS app",
            "webApp" => "Web link",
            _ => raw,
        };
    }
}
