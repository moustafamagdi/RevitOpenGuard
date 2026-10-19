[CmdletBinding()]
param([switch]$Build)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $root 'installer\dist\Revit-Open-Guard-2022-2027-Setup.exe'
if ($Build -or !(Test-Path -LiteralPath $setup)) { & "$root\installer\Build.ps1" }
# Interactive installer: Revit can remain open and manifests are published last.
& $setup
