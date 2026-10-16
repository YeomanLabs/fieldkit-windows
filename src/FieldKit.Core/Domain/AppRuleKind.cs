namespace FieldKit.Core.Domain;

/// <summary>What a Win32 app's detection or requirement rule looks at.</summary>
public enum AppRuleKind
{
    Other,
    File,
    Registry,
    ProductCode,
    Script,
}
