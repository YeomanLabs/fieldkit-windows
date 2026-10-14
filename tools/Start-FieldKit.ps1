<#
.SYNOPSIS
    Builds and launches FieldKit.

.DESCRIPTION
    FieldKit is an unpackaged, self-contained app, so this just stops a copy running from the build
    output (it locks the files; an installed FieldKit is left running), builds, and starts
    FieldKit.exe. -Demo passes --demo, which opens on "Enter demo" with sample data. -DataFolder
    keeps this copy's fieldkit.db somewhere else (Debug builds only), so demo lookups don't land in
    the installed app's recents.

.EXAMPLE
    pwsh tools\Start-FieldKit.ps1 -Demo
.EXAMPLE
    pwsh tools\Start-FieldKit.ps1 -NoBuild
.EXAMPLE
    pwsh tools\Start-FieldKit.ps1 -Demo -DataFolder $env:TEMP\FieldKitDemo
#>
param(
    [switch]$Demo,
    [switch]$NoBuild,
    [ValidateSet('', 'light', 'dark')]
    [string]$Theme = '',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [string]$Configuration = 'Debug',
    [string]$DataFolder = ''
)

$ErrorActionPreference = 'Stop'
$windows = Resolve-Path (Join-Path $PSScriptRoot '..')

$output = Join-Path $windows 'src\FieldKit.App\bin'
Get-Process -Name FieldKit -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($output, [StringComparison]::OrdinalIgnoreCase) } |
    Stop-Process -Force

if (-not $NoBuild) {
    dotnet build (Join-Path $windows 'FieldKit.sln') -c $Configuration -p:Platform=$Platform -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. For XAML errors (WMC9999), run tools\Show-XamlErrors.ps1.' }
}

$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
$exe = Join-Path $windows "src\FieldKit.App\bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\$rid\FieldKit.exe"
$arguments = @()
if ($Demo) { $arguments += '--demo' }
if ($Theme) { $arguments += @('--theme', $Theme) }
$env:FIELDKIT_DATA_FOLDER = $DataFolder
$process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
"Started $exe as process $($process.Id)$(if ($Demo) { ' with --demo' })"
