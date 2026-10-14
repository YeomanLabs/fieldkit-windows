<#
.SYNOPSIS
    Builds FieldKit's release files: an MSI and a portable zip for x64 and ARM64, plus SHA-256 sums.

.DESCRIPTION
    For each platform: a self-contained Release publish (.NET and the Windows App SDK included, no
    debug symbols), zipped as is, then packed into a per-machine MSI with WiX
    (installer\FieldKit.Installer.wixproj). Output goes to artifacts\<version>\.
    The version comes from FieldKit.App.csproj unless -Version is given.

.EXAMPLE
    pwsh tools\Build-Release.ps1
.EXAMPLE
    pwsh tools\Build-Release.ps1 -Platforms x64
#>
param(
    [string]$Version,
    [ValidateSet('x64', 'ARM64')]
    [string[]]$Platforms = @('x64', 'ARM64')
)

$ErrorActionPreference = 'Stop'
$windows = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$app = Join-Path $windows 'src\FieldKit.App\FieldKit.App.csproj'
if (-not $Version) { $Version = ([xml](Get-Content $app -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
$out = Join-Path $windows "artifacts\$Version"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

Get-Process -Name FieldKit -ErrorAction SilentlyContinue | Stop-Process -Force

foreach ($platform in $Platforms) {
    $rid = if ($platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
    $arch = $platform.ToLowerInvariant()
    $publish = Join-Path $windows "artifacts\publish\$arch"
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

    "== ${platform}: publish"
    dotnet publish $app -c Release -p:Platform=$platform -r $rid --self-contained -p:Version=$Version `
        -p:DebugType=None -p:DebugSymbols=false -o $publish -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $platform." }
    if (-not (Test-Path (Join-Path $publish 'FieldKit.exe'))) { throw "No FieldKit.exe in $publish." }

    "== ${platform}: zip"
    $zip = Join-Path $out "FieldKit-$Version-$arch.zip"
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal

    "== ${platform}: msi"
    dotnet build (Join-Path $windows 'installer\FieldKit.Installer.wixproj') -c Release -p:Platform=$platform `
        -p:Version=$Version -p:PublishDir="$publish\" -o (Join-Path $windows "artifacts\installer\$arch") -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed for $platform." }
    Copy-Item (Join-Path $windows "artifacts\installer\$arch\FieldKit.msi") (Join-Path $out "FieldKit-$Version-$arch.msi")
}

# Checksums, so a download can be checked: Get-FileHash <file> -Algorithm SHA256
$sums = Get-ChildItem $out -File | Where-Object { $_.Extension -in '.msi', '.zip' } | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
Set-Content -Path (Join-Path $out 'SHA256SUMS.txt') -Value $sums -Encoding ascii

Get-ChildItem $out | Select-Object Name, @{ Name = 'MB'; Expression = { [Math]::Round($_.Length / 1MB, 1) } }
