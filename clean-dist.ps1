[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

$repoRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) { (Get-Location).Path } else { $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "dist"
}

$targets = @(
    (Join-Path $OutputRoot "publish"),
    (Join-Path $OutputRoot "publish-installer"),
    (Join-Path $OutputRoot "Navlight-Registration-Release"),
    (Join-Path $OutputRoot "Navlight-Release")
)

$files = @(
    (Join-Path $OutputRoot "navlight-release-win-x64.zip")
)

$installerFiles = @()
$installerFiles += Get-ChildItem -Path $OutputRoot -Filter "navlight-registration-installer-*.exe" -File -ErrorAction SilentlyContinue
$installerFiles += Get-ChildItem -Path $OutputRoot -Filter "navreg-installer-*.exe" -File -ErrorAction SilentlyContinue

foreach ($target in $targets) {
    if (Test-Path -LiteralPath $target) {
        if ($PSCmdlet.ShouldProcess($target, "Remove stale dist folder")) {
            Remove-Item -LiteralPath $target -Recurse -Force
        }
    }
}

foreach ($file in $files) {
    if (Test-Path -LiteralPath $file) {
        if ($PSCmdlet.ShouldProcess($file, "Remove legacy dist file")) {
            Remove-Item -LiteralPath $file -Force
        }
    }
}

foreach ($installerFile in $installerFiles) {
    if ($PSCmdlet.ShouldProcess($installerFile.FullName, "Remove renamed installer file if stale")) {
        Remove-Item -LiteralPath $installerFile.FullName -Force
    }
}

Write-Host "Dist cleanup complete."
