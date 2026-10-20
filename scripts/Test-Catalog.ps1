param([string]$PrivateKeyPath="$PSScriptRoot\..\artifacts\keys\catalog-private.xml",[switch]$Online)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!(Test-Path $PrivateKeyPath)){throw 'Developer signing key is required for signed-update lifecycle tests.'}
foreach($framework in @('net48','net8.0-windows','net10.0-windows')) {
 & dotnet build "$root\tests\CatalogTests.csproj" -c Release --framework $framework --nologo --verbosity minimal
 if($LASTEXITCODE -ne 0){throw "Test build failed: $framework"}
 $test=Join-Path $root "tests\bin\Release\$framework\CatalogTests"
 $arguments=@($root,$PrivateKeyPath)
 if($Online){$arguments+='--online'}
 if($framework -eq 'net48'){ & "$test.exe" @arguments }else{ & dotnet "$test.dll" @arguments }
 if($LASTEXITCODE -ne 0){throw "Catalog tests failed: $framework"}
}
