param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root "artifacts"
$stage = Join-Path $artifacts "C3DTools-$Version"
$bundle = Join-Path $stage "C3DTools.bundle"
$contents = Join-Path $bundle "Contents"
$buildOut = Join-Path $root "src/C3DTools.Civil2021/bin/$Configuration/net48"

dotnet build (Join-Path $root "src/C3DTools.Civil2021/C3DTools.Civil2021.csproj") -c $Configuration -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $contents | Out-Null

# -Encoding UTF8 on both sides: the manifest contains Vietnamese text.
$manifest = Get-Content (Join-Path $root "bundle/PackageContents.xml") -Raw -Encoding UTF8
$manifest = $manifest -replace 'AppVersion="[^"]*"', "AppVersion=`"$Version`""
Set-Content (Join-Path $bundle "PackageContents.xml") $manifest -Encoding UTF8

Copy-Item (Join-Path $buildOut "*.dll") $contents
foreach ($f in "install.ps1", "install.cmd", "uninstall.cmd") {
    Copy-Item (Join-Path $root "bundle/$f") $stage
}
Copy-Item (Join-Path $root "THIRD_PARTY.md") $stage

$autodesk = Get-ChildItem $contents -Filter *.dll | Where-Object { $_.Name -match '^(Ac|Aec|Adw|AdUi)' }
if ($autodesk) { throw "Autodesk DLLs must not be packaged: $($autodesk.Name -join ', ')" }

$zip = Join-Path $artifacts "C3DTools-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Write-Host "Package: $zip"
