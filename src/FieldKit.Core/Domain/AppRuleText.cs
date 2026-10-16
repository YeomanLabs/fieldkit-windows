namespace FieldKit.Core.Domain;

/// <summary>A Win32 app's detection and requirement rules in plain words.</summary>
public static class AppRuleText
{
    public static string Describe(AppRule rule) =>
        rule.Kind switch
        {
            AppRuleKind.File => File(rule),
            AppRuleKind.Registry => Registry(rule),
            AppRuleKind.ProductCode => ProductCode(rule),
            AppRuleKind.Script => Script(rule),
            _ => "A rule FieldKit can't read",
        };

    /// <summary>The folder and name joined: "C:\Program Files\Contoso\VPN\vpnclient.exe".</summary>
    public static string FullPath(AppRule rule)
    {
        var folder = rule.Path.TrimEnd('\\');
        if (rule.Name.Length == 0) return folder;
        return folder.Length == 0 ? rule.Name : $"{folder}\\{rule.Name}";
    }

    /// <summary>Whether the rule looks somewhere machine-wide (Program Files or HKEY_LOCAL_MACHINE).</summary>
    public static bool LooksMachineWide(AppRule rule) =>
        rule.Kind switch
        {
            AppRuleKind.File => rule.Path.Contains("Program Files", StringComparison.OrdinalIgnoreCase)
                || rule.Path.Contains("%ProgramFiles", StringComparison.OrdinalIgnoreCase),
            AppRuleKind.Registry => rule.Path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)
                || rule.Path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase),
            AppRuleKind.ProductCode => true,
            _ => false,
        };

    private static string File(AppRule rule)
    {
        var target = FullPath(rule);
        var text = rule.Operation.ToLowerInvariant() switch
        {
            "exists" => $"{target} exists",
            "doesnotexist" => $"{target} doesn't exist",
            "version" => $"{target} has file version {Versioned(rule)}",
            "appversion" => $"{target} has app version {Versioned(rule)}",
            "modifieddate" => $"{target} was modified {Dated(rule)}",
            "createddate" => $"{target} was created {Dated(rule)}",
            "sizeinmb" => $"{target} is {Amount(rule)} MB",
            "sizeinbytes" => $"{target} is {Amount(rule)} bytes",
            _ => $"{target} is checked",
        };
        return rule.Check32BitOn64System ? $"{text} (looking where a 32-bit app goes on 64-bit Windows)" : text;
    }

    private static string Registry(AppRule rule)
    {
        var target = rule.Name.Length == 0 ? $"Registry key {rule.Path}" : $"Registry value {rule.Path}\\{rule.Name}";
        var text = rule.Operation.ToLowerInvariant() switch
        {
            "exists" => $"{target} exists",
            "doesnotexist" => $"{target} doesn't exist",
            "string" => $"{target} {Texted(rule)}",
            "integer" => $"{target} is {Amount(rule)}",
            "version" or "appversion" => $"{target} is version {Versioned(rule)}",
            _ => $"{target} is checked",
        };
        return rule.Check32BitOn64System ? $"{text} (in the 32-bit registry view on 64-bit Windows)" : text;
    }

    private static string ProductCode(AppRule rule)
    {
        var text = $"MSI product {rule.ProductCode} is installed";
        return HasComparison(rule) ? $"{text}, version {Versioned(rule)}" : text;
    }

    private static string Script(AppRule rule)
    {
        if (!rule.IsRequirement)
        {
            return "A PowerShell script decides. Intune counts the app as installed only when the script exits with 0 and writes something to standard output.";
        }
        if (!HasComparison(rule)) return "A PowerShell script decides";
        var output = rule.Operation.ToLowerInvariant() switch
        {
            "version" => $"version {Versioned(rule)}",
            "integer" or "float" => Amount(rule),
            "datetime" => Dated(rule),
            _ => Texted(rule),
        };
        return $"A PowerShell script's output is {output}";
    }

    private static bool HasComparison(AppRule rule) =>
        rule.Operator.Length > 0 && !rule.Operator.Equals("notConfigured", StringComparison.OrdinalIgnoreCase);

    /// <summary>"4.2.0 or later", "earlier than 5.0".</summary>
    private static string Versioned(AppRule rule) =>
        rule.Operator.ToLowerInvariant() switch
        {
            "notequal" => $"other than {rule.Value}",
            "greaterthan" => $"later than {rule.Value}",
            "greaterthanorequal" => $"{rule.Value} or later",
            "lessthan" => $"earlier than {rule.Value}",
            "lessthanorequal" => $"{rule.Value} or earlier",
            _ => rule.Value,
        };

    /// <summary>"at least 10", "exactly 3".</summary>
    private static string Amount(AppRule rule) =>
        rule.Operator.ToLowerInvariant() switch
        {
            "notequal" => $"not {rule.Value}",
            "greaterthan" => $"more than {rule.Value}",
            "greaterthanorequal" => $"at least {rule.Value}",
            "lessthan" => $"less than {rule.Value}",
            "lessthanorequal" => $"at most {rule.Value}",
            _ => $"exactly {rule.Value}",
        };

    /// <summary>"on or after 2026-01-01".</summary>
    private static string Dated(AppRule rule) =>
        rule.Operator.ToLowerInvariant() switch
        {
            "notequal" => $"not on {rule.Value}",
            "greaterthan" => $"after {rule.Value}",
            "greaterthanorequal" => $"on or after {rule.Value}",
            "lessthan" => $"before {rule.Value}",
            "lessthanorequal" => $"on or before {rule.Value}",
            _ => $"on {rule.Value}",
        };

    /// <summary>"is “Contoso”", "isn't “Contoso”".</summary>
    private static string Texted(AppRule rule) =>
        rule.Operator.ToLowerInvariant() switch
        {
            "notequal" => $"isn't “{rule.Value}”",
            "equal" or "notconfigured" or "" => $"is “{rule.Value}”",
            _ => $"compares {Amount(rule)}",
        };
}
