param([switch]$Online)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($framework in @('net48','net8.0-windows','net10.0-windows')) {
 $arguments=@('run','--project',"$root\tests-notifications\NotificationTests.csproj",'--framework',$framework,'--')
 if($Online){$arguments+='--online'}
 & dotnet @arguments
 if($LASTEXITCODE -ne 0){throw "Notification tests failed: $framework"}
}
