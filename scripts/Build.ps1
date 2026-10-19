[CmdletBinding()]
param(
 [ValidateSet('Debug','Release')][string]$Configuration='Release',
 [ValidateRange(2022,2027)][int[]]$Years=@(2022,2023,2024,2025,2026,2027),
 [string]$RevitInstallPath=''
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($year in $Years) {
 $buildArgs=@('build',"$root\RevitOpenGuard\RevitOpenGuard.csproj",'-c',$Configuration,"-p:RevitYear=$year","-p:BaseIntermediateOutputPath=obj\$year\",'--nologo','--verbosity','minimal')
 if($RevitInstallPath) {
  if($Years.Count -ne 1) { throw 'Use -Years with one year when specifying RevitInstallPath.' }
  $buildArgs += "-p:RevitInstallPath=$RevitInstallPath"
 }
 & dotnet @buildArgs
 if($LASTEXITCODE -ne 0) { throw "Revit $year build failed." }
}
Write-Host 'Build complete. Package with .\installer\Build.ps1.'
