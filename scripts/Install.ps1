[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Revit -ErrorAction SilentlyContinue) { throw 'Close all Revit instances before installing or updating Open Guard.' }
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "RevitOpenGuard\bin\$Configuration\net48"
$dll = Join-Path $source 'RevitOpenGuard.dll'
if (!(Test-Path -LiteralPath $dll)) { throw "Build first. Missing: $dll" }
$addins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
$destination = Join-Path $addins 'RevitOpenGuard'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $destination 'RevitOpenGuard.dll') -Force
$pdb = Join-Path $source 'RevitOpenGuard.pdb'
if (Test-Path -LiteralPath $pdb) { Copy-Item -LiteralPath $pdb -Destination $destination -Force }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $root 'deploy\RevitOpenGuard.addin') -Raw
$manifest.SelectSingleNode('/RevitAddIns/AddIn/Assembly').InnerText = Join-Path $destination 'RevitOpenGuard.dll'
$manifest.Save((Join-Path $addins 'RevitOpenGuard.addin'))
Write-Host "Installed: $destination"
Write-Host 'Start Revit 2024. MM Tools > Model Opening > Open Guard. First installation starts OFF; click once to enable.'
