[CmdletBinding()]
param(
    [string]$BundleZipPath,
    [string]$OutputExePath,
    [string]$MySqlDownloadUrl = "https://dev.mysql.com/get/Downloads/MySQL-8.0/mysql-8.0.42-winx64.zip",
    [string]$AppName = "Navlight Registration Installer"
)

$ErrorActionPreference = "Stop"

function Get-LatestBundleZip {
    param([string]$DistRoot)

    $candidates = Get-ChildItem -Path $DistRoot -Filter "navlight-registration-*-win-x64.zip" -File -ErrorAction SilentlyContinue |
        Sort-Object -Property LastWriteTimeUtc -Descending

    return ($candidates | Select-Object -First 1)
}

function Get-MySqlDownloadCandidates {
    param([string]$Uri)

    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($Uri) -and $Uri -match '^https://dev\.mysql\.com/get/Downloads/MySQL-([0-9]+\.[0-9]+)/(.+)$') {
        $majorMinorVersion = $Matches[1]
        $archiveName = $Matches[2]
        $candidates.Add("https://cdn.mysql.com//archives/mysql-$majorMinorVersion/$archiveName")
    }

    if (-not [string]::IsNullOrWhiteSpace($Uri)) {
        $candidates.Add($Uri)
    }

    return $candidates
}

function Download-File {
    param(
        [string[]]$Uris,
        [string]$DestinationPath
    )

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $failures = @()
    foreach ($uri in $Uris) {
        try {
            Invoke-WebRequest -Uri $uri -OutFile $DestinationPath -ErrorAction Stop
            return $uri
        }
        catch {
            $failures += "${uri}: $($_.Exception.Message)"
        }
    }

    throw "Failed to download file. Attempts:`n$($failures -join "`n")"
}

$repoRoot = $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$installerProjectRoot = Join-Path $repoRoot "Navlight-Installer\Navlight.Installer"
$installerProjectPath = Join-Path $installerProjectRoot "Navlight.Installer.csproj"
$installerPublishDir = Join-Path $distRoot "publish-installer"
$installerMySqlPackagePath = Join-Path $installerProjectRoot "MySqlPackage.zip"
$mysqlPackageCachePath = Join-Path $distRoot "MySqlPackage.zip"

if ([string]::IsNullOrWhiteSpace($BundleZipPath)) {
    # Always rebuild the release bundle here so the embedded payload can never be a stale
    # ZIP left over from an earlier source edit (see build-release.ps1 for the actual build).
    Write-Host "Running build-release.ps1 to produce a fresh release bundle..."
    $buildReleaseScript = Join-Path $repoRoot "build-release.ps1"
    & $buildReleaseScript
    if ($LASTEXITCODE -ne 0) {
        throw "build-release.ps1 failed with exit code $LASTEXITCODE."
    }

    $latestBundle = Get-LatestBundleZip -DistRoot $distRoot
    if (-not $latestBundle) {
        throw "Could not find a release bundle ZIP in '$distRoot' after running build-release.ps1."
    }

    $BundleZipPath = $latestBundle.FullName
}

if (-not (Test-Path -LiteralPath $BundleZipPath)) {
    throw "Bundle ZIP was not found at '$BundleZipPath'."
}

$bundleFileName = [System.IO.Path]::GetFileNameWithoutExtension($BundleZipPath)
$installerFileName = ($bundleFileName -replace '^navlight-registration-', 'navreg-installer-') + ".exe"

if ([string]::IsNullOrWhiteSpace($OutputExePath)) {
    $OutputExePath = Join-Path $distRoot $installerFileName
}

$outputDirectory = Split-Path -Path $OutputExePath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

if (Test-Path -LiteralPath $installerPublishDir) {
    Remove-Item -LiteralPath $installerPublishDir -Recurse -Force
}

if (Test-Path -LiteralPath $distRoot) {
    Get-ChildItem -Path $distRoot -Filter "navlight-registration-installer-*.exe" -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem -Path $distRoot -Filter "navreg-installer-*.exe" -File -ErrorAction SilentlyContinue | Remove-Item -Force
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $mysqlPackageCachePath)) {
    $downloadCandidates = Get-MySqlDownloadCandidates -Uri $MySqlDownloadUrl
    Write-Host "Downloading MySQL package (caching at $mysqlPackageCachePath for reuse)..."
    $downloadUri = Download-File -Uris $downloadCandidates -DestinationPath $mysqlPackageCachePath
    Write-Host "Downloaded MySQL package from $downloadUri"
}
else {
    Write-Host "Using cached MySQL package from $mysqlPackageCachePath"
}

Copy-Item -LiteralPath $mysqlPackageCachePath -Destination $installerMySqlPackagePath -Force

$embeddedBundlePath = Join-Path $installerProjectRoot "NavlightBundle.zip"
Copy-Item -LiteralPath $BundleZipPath -Destination $embeddedBundlePath -Force

try {
    if (Test-Path -LiteralPath $installerPublishDir) {
        Remove-Item -LiteralPath $installerPublishDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $installerPublishDir | Out-Null

    Write-Host "Publishing native installer EXE..."
    & dotnet publish $installerProjectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $installerPublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $publishedInstallerExe = Join-Path $installerPublishDir "navreg-installer.exe"
    if (-not (Test-Path -LiteralPath $publishedInstallerExe)) {
        throw "Expected installer EXE was not created at '$publishedInstallerExe'."
    }

    Copy-Item -LiteralPath $publishedInstallerExe -Destination $OutputExePath -Force
    Write-Host "Installer created: $OutputExePath"
    Write-Host "This EXE is the native C# installer app."
}
finally {
    if (Test-Path -LiteralPath $embeddedBundlePath) {
        Remove-Item -LiteralPath $embeddedBundlePath -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $installerMySqlPackagePath) {
        Remove-Item -LiteralPath $installerMySqlPackagePath -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $installerPublishDir) {
        Remove-Item -LiteralPath $installerPublishDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
