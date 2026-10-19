[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Revit -ErrorAction SilentlyContinue) { throw 'Close all Revit instances before uninstalling Open Guard.' }
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
$folder = Join-Path $addins 'RevitOpenGuard'
foreach ($file in @((Join-Path $addins 'RevitOpenGuard.addin'), (Join-Path $folder 'RevitOpenGuard.dll'), (Join-Path $folder 'RevitOpenGuard.pdb'))) {
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
}
if ((Test-Path -LiteralPath $folder) -and @(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) { Remove-Item -LiteralPath $folder }
Write-Host 'Open Guard uninstalled. Saved settings and logs have been preserved.'
