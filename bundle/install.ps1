param([switch]$Uninstall)
$ErrorActionPreference = "Stop"

$target = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\C3DTools.bundle"

if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw "Close Civil 3D before installing or uninstalling C3DTools."
}

if (Test-Path $target) { Remove-Item $target -Recurse -Force }

if ($Uninstall) {
    Write-Host "C3DTools removed."
    return
}

Copy-Item (Join-Path $PSScriptRoot "C3DTools.bundle") $target -Recurse
# Downloaded files are blocked by Windows; AutoCAD cannot load blocked DLLs.
Get-ChildItem $target -Recurse -File | Unblock-File
Write-Host "C3DTools installed to $target"
Write-Host "Open Civil 3D 2021 and type CTHELLO."
