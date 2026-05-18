param(
    [int]$Port = 3306
)

$ErrorActionPreference = "Stop"

$databaseRoot = $PSScriptRoot
$mysqlRoot = Join-Path $databaseRoot "mysql"
$binRoot = Join-Path $mysqlRoot "bin"
$dataRoot = Join-Path $databaseRoot "data"
$mysqldPath = Join-Path $binRoot "mysqld.exe"

if (-not (Test-Path -LiteralPath $mysqldPath)) {
    throw "mysqld.exe was not found at '$mysqldPath'."
}

if (-not (Test-Path -LiteralPath $dataRoot)) {
    throw "The data directory was not found at '$dataRoot'. Run setup-mysql.ps1 first."
}

$dataFiles = Get-ChildItem -Path $dataRoot -Force -ErrorAction SilentlyContinue
if (-not $dataFiles) {
    throw "The data directory is empty. Run setup-mysql.ps1 first."
}

$existingListener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($existingListener) {
    throw "Port $Port is already in use by PID $($existingListener.OwningProcess)."
}

Write-Host "Starting MySQL on port $Port..."
Write-Host "Leave this window open while the Navlight app is using the database."

& $mysqldPath --console --basedir="$mysqlRoot" --datadir="$dataRoot" --port=$Port
