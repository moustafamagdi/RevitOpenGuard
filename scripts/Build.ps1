[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$RevitInstallPath = 'C:\Program Files\Autodesk\Revit 2024'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
foreach ($file in @('RevitAPI.dll', 'RevitAPIUI.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $RevitInstallPath $file))) { throw "Missing $file in $RevitInstallPath. Set -RevitInstallPath correctly." }
}
$solution = Join-Path $root 'RevitOpenGuard.sln'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = $null
if (Test-Path -LiteralPath $vswhere) {
    $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if ($msbuild) {
    & $msbuild $solution '/restore' '/t:Rebuild' "/p:Configuration=$Configuration" "/p:RevitInstallPath=$RevitInstallPath" '/nologo' '/verbosity:minimal'
} elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
    & dotnet build $solution --configuration $Configuration --no-incremental "/p:RevitInstallPath=$RevitInstallPath" --verbosity minimal
} else {
    throw 'Install Visual Studio 2022 with .NET desktop development and the .NET Framework 4.8 targeting pack.'
}
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE). No installation performed." }
Write-Host "Build finished. Next, with Revit closed: .\scripts\Install.ps1 -Configuration $Configuration"
