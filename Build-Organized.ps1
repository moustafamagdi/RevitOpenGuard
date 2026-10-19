$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = if (Test-Path $vswhere) { & $vswhere -latest -products '*' -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
dotnet build (Join-Path $PSScriptRoot 'RevitOpenGuard/RevitOpenGuard.csproj') -c Release --nologo -p:DeployAddin=false -p:DeployPlugin=false -p:RevitVersion=2024
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
