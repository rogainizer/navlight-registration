param(
    [SecureString]$RootPassword = (ConvertTo-SecureString "root" -AsPlainText -Force),
    [int]$Port = 3306
)

$ErrorActionPreference = "Stop"

$databaseRoot = $PSScriptRoot
$mysqlRoot = Join-Path $databaseRoot "mysql"
$binRoot = Join-Path $mysqlRoot "bin"
$mysqlAdminPath = Join-Path $binRoot "mysqladmin.exe"

if (-not (Test-Path -LiteralPath $mysqlAdminPath)) {
    throw "mysqladmin.exe was not found at '$mysqlAdminPath'."
}

$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if (-not $listener) {
    Write-Host "No process is listening on port $Port."
    return
}

$credential = New-Object System.Management.Automation.PSCredential "unused", $RootPassword
$plainRootPassword = $credential.GetNetworkCredential().Password

Write-Host "Stopping MySQL on port $Port..."
& $mysqlAdminPath --protocol=TCP --port=$Port -u root "--password=$plainRootPassword" shutdown

if ($LASTEXITCODE -ne 0) {
    throw "mysqladmin shutdown failed with exit code $LASTEXITCODE."
}

Write-Host "MySQL stopped successfully."
