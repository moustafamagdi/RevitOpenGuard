[CmdletBinding()]
param([string]$OutputDirectory="$PSScriptRoot\dist",[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$SkipBuild) { & "$root\scripts\Build.ps1" }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$stage=Join-Path ([IO.Path]::GetTempPath()) ('OpenGuardPackage-'+[guid]::NewGuid().ToString('N'))
try {
 New-Item -ItemType Directory $stage | Out-Null
 foreach($year in 2022..2027) {
  $framework=if($year -le 2024){'net48'}elseif($year -le 2026){'net8.0-windows'}else{'net10.0-windows'}
  $source=Join-Path $root "RevitOpenGuard\bin\$year\Release\$framework"
  $destination=New-Item -ItemType Directory (Join-Path $stage $year)
  Copy-Item "$source\RevitOpenGuard.dll" $destination
  if(Test-Path "$source\RevitOpenGuard.deps.json") { Copy-Item "$source\RevitOpenGuard.deps.json" $destination }
 }
 Copy-Item "$root\deploy\RevitOpenGuard.addin" $stage
 $payload=Join-Path (Split-Path $stage) ((Split-Path $stage -Leaf)+'.zip')
 Compress-Archive -Path "$stage\*" -DestinationPath $payload
 $compiler="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
 $exe=Join-Path $OutputDirectory 'Revit-Open-Guard-2022-2027-Setup.exe'
 & $compiler /nologo /target:winexe /platform:x64 "/out:$exe" "/resource:$payload,Payload" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.IO.Compression.dll "$PSScriptRoot\Setup.cs"
 if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
 $test=Start-Process -FilePath $exe -ArgumentList '/selftest' -WindowStyle Hidden -Wait -PassThru
 if($test.ExitCode -ne 0){throw 'Installer self-test failed. See %TEMP%\RevitOpenGuard-setup-test.txt'}
 Get-FileHash $exe -Algorithm SHA256
} finally {
 $resolved=[IO.Path]::GetFullPath($stage)
 if($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -like 'OpenGuardPackage-*') { Remove-Item -LiteralPath $resolved -Recurse -Force }
 if($payload -and (Test-Path -LiteralPath $payload)) { Remove-Item -LiteralPath $payload -Force }
}
