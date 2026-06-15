[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputRoot = (Join-Path $PSScriptRoot "dist")
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$registrationRoot = Join-Path $repoRoot "Navlight-Registration"
$hostRoot = Join-Path $repoRoot "Navlight-Host"
$projectPath = Join-Path $registrationRoot "Navlight.Registration.App\Navlight.Registration.App.csproj"
$publishDir = Join-Path $OutputRoot "publish"
$bundleRoot = Join-Path $OutputRoot "Navlight-Registration-Release"
$payloadRoot = Join-Path $bundleRoot "payload"
$zipPath = Join-Path $OutputRoot "navlight-registration-$RuntimeIdentifier.zip"
$releaseVersion = & git -C $repoRoot describe --tags --exact-match 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseVersion)) {
    $releaseVersion = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
}
if ([string]::IsNullOrWhiteSpace($releaseVersion)) {
    $releaseVersion = "unknown"
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $bundleRoot) {
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $publishDir | Out-Null
New-Item -ItemType Directory -Path $payloadRoot | Out-Null

Write-Host "Publishing Navlight Registration app..."
& dotnet publish $projectPath -c $Configuration -r $RuntimeIdentifier --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$appTargetDir = Join-Path $payloadRoot "Navlight.Registration.App"
$databaseTargetDir = Join-Path $payloadRoot "Database"
$samplesTargetDir = Join-Path $payloadRoot "Samples"

Copy-Item -Path $publishDir -Destination $appTargetDir -Recurse
New-Item -ItemType Directory -Path $databaseTargetDir | Out-Null
New-Item -ItemType Directory -Path $samplesTargetDir | Out-Null

Copy-Item -LiteralPath (Join-Path $registrationRoot "Navlight.Registration.App\appsettings.example.json") -Destination (Join-Path $appTargetDir "appsettings.example.json")
Copy-Item -LiteralPath (Join-Path $hostRoot "Database\setup-mysql.ps1") -Destination (Join-Path $databaseTargetDir "setup-mysql.ps1")
Copy-Item -LiteralPath (Join-Path $hostRoot "Database\start-mysql.ps1") -Destination (Join-Path $databaseTargetDir "start-mysql.ps1")
Copy-Item -LiteralPath (Join-Path $hostRoot "Database\stop-mysql.ps1") -Destination (Join-Path $databaseTargetDir "stop-mysql.ps1")
Copy-Item -LiteralPath (Join-Path $registrationRoot "database\schema.sql") -Destination (Join-Path $databaseTargetDir "schema.sql")
Copy-Item -LiteralPath (Join-Path $registrationRoot "database\test-data.sql") -Destination (Join-Path $databaseTargetDir "test-data.sql")
Copy-Item -LiteralPath (Join-Path $registrationRoot "Navlight.Registration.App\EntryLists\Entries.xlsx") -Destination (Join-Path $samplesTargetDir "Entries.xlsx")
Copy-Item -LiteralPath (Join-Path $registrationRoot "get-dhcp-reservation-info.ps1") -Destination (Join-Path $bundleRoot "get-dhcp-reservation-info.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "Find-NavlightReaderPort.ps1") -Destination (Join-Path $bundleRoot "Find-NavlightReaderPort.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "install-navlight.ps1") -Destination (Join-Path $bundleRoot "install-navlight.ps1")

$readmePath = Join-Path $bundleRoot "README.txt"
@"
Navlight release bundle
Version: $releaseVersion

Files:
- install-navlight.ps1 : installs ClientOnly, HostAndClient, or SingleUser roles
- get-dhcp-reservation-info.ps1 : shows the host PC IPv4 and MAC address for router DHCP reservation setup
- Find-NavlightReaderPort.ps1 : scans COM ports to find the NavLight reader
- payload\Navlight.Registration.App : published desktop app
- payload\Database : MySQL setup/start/stop scripts and schema files
- payload\Samples\Entries.xlsx : sample spreadsheet installed for HostAndClient and SingleUser

If you do not pass -InstallRoot, the installer will prompt for the install folder and suggest .\NavlightRegistration.

The installer does not require elevation unless you choose a protected install folder.

Examples:
powershell -ExecutionPolicy Bypass -File .\install-navlight.ps1 -InstallMode ClientOnly -DatabaseServer navlighthost
powershell -ExecutionPolicy Bypass -File .\install-navlight.ps1 -InstallMode HostAndClient
powershell -ExecutionPolicy Bypass -File .\install-navlight.ps1 -InstallMode SingleUser

Host installs default the app configuration to navlighthost, ask for the host name that client PCs should use, and stop if you have not already created a DHCP reservation on the router.
SingleUser installs configure the app to use localhost and do not require any router setup.
"@ | Set-Content -Path $readmePath -Encoding ASCII

Compress-Archive -Path (Join-Path $bundleRoot "*") -DestinationPath $zipPath

Write-Host "Release bundle created: $zipPath"