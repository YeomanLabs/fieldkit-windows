namespace FieldKit.Core.Domain;

/// <summary>One detection or requirement rule of a Win32 app (Graph's <c>win32LobAppRule</c>), as Intune stores it.</summary>
public sealed record AppRule
{
    public AppRuleKind Kind { get; init; }

    /// <summary>A requirement rule (decides whether the app applies); otherwise a detection rule (decides whether it's installed).</summary>
    public bool IsRequirement { get; init; }

    /// <summary>File rules: the folder. Registry rules: the key.</summary>
    public string Path { get; init; } = "";

    /// <summary>File rules: the file or folder name. Registry rules: the value name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Graph's <c>operationType</c>, e.g. "exists", "version", "string". Empty for product codes.</summary>
    public string Operation { get; init; } = "";

    /// <summary>Graph's comparison operator, e.g. "greaterThanOrEqual"; "notConfigured" or empty when there's none.</summary>
    public string Operator { get; init; } = "";

    /// <summary>What's compared against; for product codes, the version.</summary>
    public string Value { get; init; } = "";

    /// <summary>File and registry rules: look where a 32-bit app would be on 64-bit Windows.</summary>
    public bool Check32BitOn64System { get; init; }

    public string ProductCode { get; init; } = "";

    /// <summary>"C:\Program Files\Contoso\VPN\vpnclient.exe has file version 4.2.0 or later".</summary>
    public string Description => AppRuleText.Describe(this);
}
