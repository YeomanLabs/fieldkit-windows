<#
.SYNOPSIS
    Shows the real XAML compiler errors when `dotnet build` only reports WMC9999.

.DESCRIPTION
    The Windows App SDK 1.8 XAML compiler (XamlCompiler.exe, which `dotnet build` runs) embeds
    its error messages under a different resource name from the one it looks up, so every XAML
    error surfaces as "WMC9999: Could not find any resources appropriate for the specified
    culture". This script runs the same compiler through a small .NET Framework harness that
    points the lookup at the right name, and prints the real errors with file and line.

    Run it after a failed build, in Windows PowerShell (it compiles the harness with Add-Type):
        powershell -NoProfile -File tools\Show-XamlErrors.ps1
#>
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this with Windows PowerShell (powershell.exe): the harness targets .NET Framework.'
}

$project = Resolve-Path (Join-Path $PSScriptRoot '..\src\FieldKit.App')
$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
$intermediate = "obj\$Platform\$Configuration\net10.0-windows10.0.26100.0\$rid"
if (-not (Test-Path (Join-Path $project "$intermediate\input.json"))) {
    throw "No XAML compiler input in $intermediate. Build $Configuration|$Platform first."
}

# The compiler version the project restored.
$assets = Get-Content (Join-Path $project 'obj\project.assets.json') -Raw | ConvertFrom-Json
$winui = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'Microsoft.WindowsAppSDK.WinUI/*' } | Select-Object -First 1
$version = $winui.Split('/')[1]
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$tools = Join-Path $packages "microsoft.windowsappsdk.winui\$version\tools\net472"

$harnessDir = Join-Path $env:TEMP "FieldKitXamlErrors\$version"
$harness = Join-Path $harnessDir 'harness.exe'
if (-not (Test-Path $harness)) {
    New-Item -ItemType Directory -Force -Path $harnessDir | Out-Null
    Copy-Item "$tools\*" $harnessDir -Recurse -Force
    $code = @'
using System;
using System.Linq;
using System.Reflection;
using System.Resources;

public static class Harness
{
    public static int Main(string[] args)
    {
        var compiler = Assembly.LoadFrom(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XamlCompiler.exe"));
        Type[] types;
        try { types = compiler.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
        foreach (var type in types.Where(t => t.Name == "ErrorMessages"))
        {
            var field = type.GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, new ResourceManager("Microsoft.Windows.UI.Xaml.Build.Tasks.ErrorMessages", compiler));
            }
        }
        object result = compiler.EntryPoint.Invoke(null, new object[] { args });
        return result is int ? (int)result : 0;
    }
}
'@
    Add-Type -TypeDefinition $code -Language CSharp -OutputAssembly $harness -OutputType ConsoleApplication
    Copy-Item (Join-Path $harnessDir 'XamlCompiler.exe.config') "$harness.config" -Force
}

$output = Join-Path $env:TEMP 'FieldKitXamlErrors\output.json'
Push-Location $project
try {
    & $harness "$intermediate\input.json" $output | Out-Null
} finally {
    Pop-Location
}

$entries = (Get-Content $output -Raw | ConvertFrom-Json).MSBuildLogEntries | Where-Object { $_.Type -ne 0 }
if (-not $entries) {
    'No XAML errors.'
    return
}
foreach ($entry in $entries) {
    $kind = if ($entry.Type -eq 2) { 'error' } else { 'warning' }
    "{0} {1} {2}({3},{4}): {5}" -f $kind, $entry.ErrorCode, $entry.File, $entry.LineNumber, $entry.ColumnNumber, $entry.Message
}
