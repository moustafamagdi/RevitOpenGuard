[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Revit -ErrorAction SilentlyContinue) { throw 'Close all Revit instances before uninstalling Open Guard.' }
$setup = Join-Path $env:LOCALAPPDATA 'MoustafaMagdi\RevitOpenGuard\Setup\RevitOpenGuardSetup.exe'
if (!(Test-Path -LiteralPath $setup)) {
    $setup = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024\RevitOpenGuard\RevitOpenGuardSetup.exe'
}
if (!(Test-Path -LiteralPath $setup)) { throw 'Use the setup or Windows Installed apps to remove Open Guard.' }
& $setup /uninstall
