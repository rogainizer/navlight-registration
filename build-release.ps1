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
$releaseVersion = (& git -C $repoRoot tag --points-at HEAD | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($releaseVersion)) {
    $releaseVersion = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
}
if ([string]::IsNullOrWhiteSpace($releaseVersion)) {
    $releaseVersion = "unknown"
}

$releaseVersionForFileName = ($releaseVersion -replace '[<>:"/\\|?*]', '-').Trim()
if ([string]::IsNullOrWhiteSpace($releaseVersionForFileName)) {
    $releaseVersionForFileName = "unknown"
}

$zipPath = Join-Path $OutputRoot "navlight-registration-$releaseVersionForFileName-$RuntimeIdentifier.zip"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $bundleRoot) {
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}

if (Test-Path -LiteralPath $OutputRoot) {
    Get-ChildItem -Path $OutputRoot -Filter "navlight-registration-*.zip" -File | Remove-Item -Force
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
$utilitiesTargetDir = Join-Path $bundleRoot "Utilities"

Copy-Item -Path $publishDir -Destination $appTargetDir -Recurse
New-Item -ItemType Directory -Path $databaseTargetDir | Out-Null
New-Item -ItemType Directory -Path $samplesTargetDir | Out-Null
New-Item -ItemType Directory -Path $utilitiesTargetDir | Out-Null

Copy-Item -LiteralPath (Join-Path $registrationRoot "Navlight.Registration.App\appsettings.example.json") -Destination (Join-Path $appTargetDir "appsettings.example.json")
Copy-Item -LiteralPath (Join-Path $hostRoot "Database\setup-mysql.ps1") -Destination (Join-Path $databaseTargetDir "setup-mysql.ps1")
Copy-Item -LiteralPath (Join-Path $hostRoot "Database\start-mysql.ps1") -Destination (Join-Path $databaseTargetDir "start-mysql.ps1")
Copy-Item -LiteralPath (Join-Path $hostRoot "Database\stop-mysql.ps1") -Destination (Join-Path $databaseTargetDir "stop-mysql.ps1")
Copy-Item -LiteralPath (Join-Path $registrationRoot "database\schema.sql") -Destination (Join-Path $databaseTargetDir "schema.sql")
Copy-Item -LiteralPath (Join-Path $registrationRoot "database\test-data.sql") -Destination (Join-Path $databaseTargetDir "test-data.sql")
Copy-Item -LiteralPath (Join-Path $registrationRoot "Navlight.Registration.App\EntryLists\Entries.xlsx") -Destination (Join-Path $samplesTargetDir "Entries.xlsx")
Copy-Item -LiteralPath (Join-Path $registrationRoot "get-dhcp-reservation-info.ps1") -Destination (Join-Path $bundleRoot "get-dhcp-reservation-info.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "install-navlight.ps1") -Destination (Join-Path $bundleRoot "install-navlight.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "Find-NavlightReaderPort.ps1") -Destination (Join-Path $utilitiesTargetDir "Find-NavlightReaderPort.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "Read-NavlightTagId.ps1") -Destination (Join-Path $utilitiesTargetDir "Read-NavlightTagId.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "Clear-NavlightTag.ps1") -Destination (Join-Path $utilitiesTargetDir "Clear-NavlightTag.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "CP210x_Windows_Drivers.zip") -Destination (Join-Path $utilitiesTargetDir "CP210x_Windows_Drivers.zip")

$readmePath = Join-Path $bundleRoot "README.txt"
@"
Navlight release bundle
Version: $releaseVersion

Files:
- install-navlight.ps1 : installs ClientOnly, HostAndClient, or SingleUser roles
- get-dhcp-reservation-info.ps1 : shows the host PC IPv4 and MAC address for router DHCP reservation setup
- payload\Navlight.Registration.App : published desktop app
- payload\Database : MySQL setup/start/stop scripts and schema files
- payload\Samples\Entries.xlsx : sample spreadsheet installed for HostAndClient and SingleUser
- Utilities\Find-NavlightReaderPort.ps1 : scans COM ports to find the NavLight reader
- Utilities\Read-NavlightTagId.ps1 : reads the current NavLight tag contents
- Utilities\Clear-NavlightTag.ps1 : clears the current NavLight tag contents
- Utilities\CP210x_Windows_Drivers.zip : Silicon Labs USB serial drivers for the reader

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