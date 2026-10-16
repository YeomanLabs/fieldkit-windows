using System.Text.Json.Serialization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// One of a Win32 app's <c>rules</c>: a file, registry, product code or PowerShell script rule,
/// for detection or as a requirement. The script itself (<c>scriptContent</c>) isn't read.
/// </summary>
internal sealed class Win32LobAppRuleDto
{
    [JsonPropertyName("@odata.type")]
    public string? Type { get; init; }

    public string? RuleType { get; init; }

    public string? Path { get; init; }

    public string? FileOrFolderName { get; init; }

    public string? KeyPath { get; init; }

    public string? ValueName { get; init; }

    public bool? Check32BitOn64System { get; init; }

    public string? OperationType { get; init; }

    public string? Operator { get; init; }

    public string? ComparisonValue { get; init; }

    public string? ProductCode { get; init; }

    public string? ProductVersionOperator { get; init; }

    public string? ProductVersion { get; init; }

    public AppRule ToDomain()
    {
        var kind = Type?.Replace("#microsoft.graph.", "", StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "win32lobappfilesystemrule" => AppRuleKind.File,
            "win32lobappregistryrule" => AppRuleKind.Registry,
            "win32lobappproductcoderule" => AppRuleKind.ProductCode,
            "win32lobapppowershellscriptrule" => AppRuleKind.Script,
            _ => AppRuleKind.Other,
        };
        return new AppRule
        {
            Kind = kind,
            IsRequirement = string.Equals(RuleType, "requirement", StringComparison.OrdinalIgnoreCase),
            Path = (kind == AppRuleKind.Registry ? KeyPath : Path) ?? "",
            Name = (kind == AppRuleKind.Registry ? ValueName : FileOrFolderName) ?? "",
            Operation = OperationType ?? "",
            Operator = (kind == AppRuleKind.ProductCode ? ProductVersionOperator : Operator) ?? "",
            Value = (kind == AppRuleKind.ProductCode ? ProductVersion : ComparisonValue) ?? "",
            Check32BitOn64System = Check32BitOn64System ?? false,
            ProductCode = ProductCode ?? "",
        };
    }
}
