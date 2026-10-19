[CmdletBinding()]
param(
 [string]$CatalogPath = "$PSScriptRoot\..\catalog\catalog.json",
 [string]$PrivateKeyPath = "$PSScriptRoot\..\artifacts\keys\catalog-private.xml",
 [string]$OutputPath = "$PSScriptRoot\..\catalog\catalog.signed.json",
 [switch]$Publish
)
$ErrorActionPreference = 'Stop'
# Sign the exact UTF-8 payload bytes; the private key is never included in the package.
$payload = [IO.File]::ReadAllBytes((Resolve-Path $CatalogPath))
$data = [Text.Encoding]::UTF8.GetString($payload).TrimStart([char]0xFEFF) | ConvertFrom-Json
if ($data.schemaVersion -ne 1 -or !$data.version -or !$data.rules) { throw 'Invalid catalog.' }
$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp = $false
try {
 $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path $PrivateKeyPath)))
 if ($rsa.ToXmlString($false).Trim() -ne [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\catalog\public-key.xml')).Trim()) {
  throw 'The private key does not match the public key embedded in the add-in.'
 }
 $signature = $rsa.SignData($payload, [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
 $envelope = @{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($signature)} | ConvertTo-Json -Compress
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$envelope,(New-Object Text.UTF8Encoding($false)))
 Write-Host "Signed catalog $($data.version): $OutputPath"
} finally { $rsa.Dispose() }
if ($Publish) {
 $sourceRoot=Split-Path -Parent $PSScriptRoot
 & dotnet build "$sourceRoot\tests\CatalogTests.csproj" -c Release --framework net48 --nologo --verbosity quiet
 if($LASTEXITCODE -ne 0){throw 'Cannot build the catalog validator.'}
 & "$sourceRoot\tests\bin\Release\net48\CatalogTests.exe" --verify ([IO.Path]::GetFullPath($OutputPath))
 if($LASTEXITCODE -ne 0){throw 'The signed catalog did not pass the same validation used by installed clients.'}
 $remote=& git -C $sourceRoot remote get-url origin
 if($LASTEXITCODE -ne 0){throw 'Cannot read the repository remote.'}
 $publishRoot=Join-Path ([IO.Path]::GetTempPath()) ('OpenGuardFeed-'+[guid]::NewGuid().ToString('N'))
 try {
  New-Item -ItemType Directory -Path $publishRoot | Out-Null
  & git -C $publishRoot init -b catalog-updates --quiet
  & git -C $publishRoot remote add origin $remote
  $name=& git -C $sourceRoot config user.name
  $email=& git -C $sourceRoot config user.email
  if(!$name -or !$email){throw 'Configure git user.name and user.email before publishing.'}
  & git -C $publishRoot config user.name $name
  & git -C $publishRoot config user.email $email
  $existing=& git -C $publishRoot ls-remote --heads origin refs/heads/catalog-updates
  if($LASTEXITCODE -ne 0){throw 'Cannot connect to the catalog repository.'}
  if($existing) {
   & git -C $publishRoot fetch --depth 1 origin catalog-updates --quiet
   if($LASTEXITCODE -ne 0){throw 'Cannot fetch the catalog branch.'}
   & git -C $publishRoot checkout -B catalog-updates FETCH_HEAD --quiet
   if($LASTEXITCODE -ne 0){throw 'Cannot prepare the catalog branch.'}
   $old=Get-Content (Join-Path $publishRoot 'catalog.json') -Raw | ConvertFrom-Json
   if([version]$data.version -le [version]$old.version){throw 'Increase the catalog version before publishing an update.'}
  }
  Copy-Item -LiteralPath $OutputPath -Destination (Join-Path $publishRoot 'catalog.signed.json')
  Copy-Item -LiteralPath $CatalogPath -Destination (Join-Path $publishRoot 'catalog.json')
  Copy-Item -LiteralPath (Join-Path $sourceRoot 'catalog\public-key.xml') -Destination $publishRoot
  Set-Content (Join-Path $publishRoot 'README.md') 'Signed Open Guard error catalogs. Source, signing instructions and installer are maintained in the main development checkout. Never publish a private signing key.'
  & git -C $publishRoot add -- catalog.json catalog.signed.json public-key.xml README.md
  & git -C $publishRoot commit -m "Publish Open Guard catalog $($data.version)" --quiet
  if($LASTEXITCODE -ne 0){throw 'Cannot commit the catalog update.'}
  & git -C $publishRoot push origin HEAD:refs/heads/catalog-updates
  if($LASTEXITCODE -ne 0){throw 'Catalog push failed; the local signed file was retained.'}
  Write-Host 'Published to the catalog-updates branch. Installed clients can now check this feed.'
 } finally {
  $resolved=[IO.Path]::GetFullPath($publishRoot)
  if($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -like 'OpenGuardFeed-*') {
   Remove-Item -LiteralPath $resolved -Recurse -Force
  }
 }
}
